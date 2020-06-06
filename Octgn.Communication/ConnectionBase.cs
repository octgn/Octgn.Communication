using System;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using Octgn.Communication.Utility;

namespace Octgn.Communication
{
    public abstract class ConnectionBase : IConnection
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(ConnectionBase));
#pragma warning restore IDE1006 // Naming Styles

        public static TimeSpan WaitForResponseTimeout { get; set; } = TimeSpan.FromSeconds(15);
        public static TimeSpan WaitToConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

        #region Identification
        public string ConnectionId { get; } = UID.Generate(Interlocked.Increment(ref _nextSeed));

        private static int _nextSeed = 0;

        public bool Equals(IConnection other) {
            if (other == null || this == null) return false;
            return other.ConnectionId == this.ConnectionId;
        }
        #endregion

        public string RemoteAddress { get; }

        public User User { get; private set; }

        protected IHandshaker Handshaker { get; }

        protected ISerializer Serializer { get; }

        public Server Server { get; }

        public Client Client { get; }

        protected ConnectionBase(string remoteAddress, IHandshaker handshaker, ISerializer serializer, Server server) {
            if (string.IsNullOrWhiteSpace(remoteAddress)) throw new ArgumentNullException(remoteAddress);

            Handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));

            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            Server = server ?? throw new ArgumentNullException(nameof(server));

            RemoteAddress = remoteAddress;

            _toString = $"{{0}}:{ConnectionId}:{RemoteAddress}:{{1}}";
        }

        protected ConnectionBase(string remoteAddress, IHandshaker handshaker, ISerializer serializer, Client client) {
            if (string.IsNullOrWhiteSpace(remoteAddress)) throw new ArgumentNullException(remoteAddress);

            Handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));

            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            Client = client ?? throw new ArgumentNullException(nameof(client));

            RemoteAddress = remoteAddress;

            _toString = $"{{0}}:{ConnectionId}:{RemoteAddress}:{{1}}";
        }

        private readonly string _toString;
        public override string ToString() {
            var parent =
                Server != null ? "S"
                : Client != null ? "C"
                : string.Empty;

            var str = string.Format(_toString, parent, (object)User ?? string.Empty);

            return str;
        }

        #region State Machine

        public ConnectionState State => _state;

        /// <summary>
        /// This value should only be set in <see cref="TryTransitionState(ConnectionState, out ConnectionState, bool)"/>
        /// </summary>
        private ConnectionState _state = ConnectionState.Created;

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        private readonly object _stateLocker = new object();

        /// <summary>
        /// Transition from the current <see cref="ConnectionState"/> to the <paramref name="newState"/>.
        /// Returns true if the state was transitioned.
        /// If the transition is invalid, <see cref="TransitionInvalidException"/> will be thrown.
        /// </summary>
        /// <param name="newState">The new state to transition to.</param>
        /// <exception cref="InvalidOperationException">If the transition is not valid (determined by <see cref="IsStateTransitionValid(ConnectionState, ConnectionState, bool)"/>)</exception>
        protected bool TransitionState(ConnectionState newState) {
            var args = new ConnectionStateChangedEventArgs {
                Connection = this,
                NewState = newState,
            };

            bool stateTransitioned = false;

            lock (_stateLocker) {
                ConnectionState oldState = State;

                // The state didn't transition.
                if (oldState == newState) return false;

                if (!IsStateTransitionValid(oldState, newState))
                    throw new TransitionInvalidException(oldState, newState);

                // The transition is valid, lets continue.

                // Have to set this in the lock statement in case the value changed
                args.OldState = oldState;

                // Set the new state
                _state = newState;

                stateTransitioned = oldState != _state;
            }

            if (stateTransitioned)
                OnConnectionStateChanged(this, args);

            return stateTransitioned;
        }

        protected virtual void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs args) {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1))) {
                try {
                    Task.Run(() => {
                        ConnectionStateChanged?.Invoke(this, args);
                    }, cts.Token).GetAwaiter().GetResult();
                } catch (TaskCanceledException) {
                    throw new InvalidOperationException("Deadlock detected");
                }
            }

            if (args.NewState == ConnectionState.Closed) {
                try {
                    _closedCancellation?.Cancel();
                } catch (ObjectDisposedException) {
                    Log.Info($"{this}: {nameof(TransitionState)}: close cancellation disposed");
                }
            }
        }

        protected virtual bool IsStateTransitionValid(ConnectionState oldState, ConnectionState newState) {
            // Transitioning from any state to closed is allowed right now
            if (newState == ConnectionState.Closed) return true;

            // Covers pretty much all cases really well for now.
            return newState == oldState + 1;
        }

        #endregion State Machine

        public HandshakeResult HandshakeResult { get; private set; }

        public async Task Connect(CancellationToken cancellationToken = default) {
            TransitionState(ConnectionState.Connecting);
            try {
                await ConnectImpl(cancellationToken);

                StartHandshaking();

                if (Handshaker != null) {
                    Log.Info($"{this}: Handshaking...");

                    HandshakeResult = await Handshaker.Handshake(this, cancellationToken);

                    if (!HandshakeResult.Successful) {
                        var ex = new HandshakeException(HandshakeResult.ErrorCode);
                        Log.Info($"{this}: Handshake Failed {HandshakeResult.ErrorCode}: {ex.Message}");
                        throw ex;
                    }

                    User = HandshakeResult.User;
                }

                TransitionState(ConnectionState.Connected);
            } catch (Exception) {
                TransitionState(ConnectionState.Closed);
                throw;
            }
        }

        internal void StartHandshaking() {
            TransitionState(ConnectionState.Handshaking);
        }

        protected abstract Task ConnectImpl(CancellationToken cancellationToken);

        public void Close() {
            TransitionState(ConnectionState.Closed);
        }

        public event RequestReceived RequestReceived;

        public event PacketReceived PacketReceived;

        protected async void StartProcessingReceivedData(ulong packetId, byte[] data, ISerializer serializer) {
            Log.Info($"{this}: {nameof(StartProcessingReceivedData)}");

            try {
                await ProcessReceivedData(packetId, data, serializer);
            } catch (OperationCanceledException) {
                Log.Warn($"{this}: Canceled processing packet {packetId}");
            } catch (DisconnectedException ex) {
                Log.Warn($"{this}: Disconnected while processing packet {packetId}: {ex}");
            } catch (InvalidDataException ex) {
                Log.Warn($"{this}: Dropping packet, data is invalid: {ex}");
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        protected async Task ProcessReceivedData(ulong packetId, byte[] data, ISerializer serializer) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            if (State == ConnectionState.Closed) throw new DisconnectedException();

            var serializedPacket = SerializedPacket.Read(data);

            Log.TracePacketReceived(this, serializedPacket);

            {
                var args = new PacketReceivedEventArgs() {
                    PacketId = packetId,
                    Packet = serializedPacket,
                    Connection = this
                };

                var packetEventHandler = PacketReceived;

                if (packetEventHandler != null) {
                    foreach (var handler in packetEventHandler.GetInvocationList().Cast<PacketReceived>()) {
                        if (State == ConnectionState.Closed) throw new DisconnectedException();

                        try {
                            await handler(this, args);
                        } catch (Exception ex) {
                            Log.Error($"Error in {nameof(PacketReceived)} event handler", ex);
                        }

                        if (args.IsHandled) return;
                    }
                }
            }

            if (State == ConnectionState.Closed) throw new DisconnectedException();

            Packet packet;
            try {
                packet = serializedPacket.DeserializePacket(serializer);
            } catch (Exception ex) {
                throw new InvalidDataException($"Error deserializing {serializedPacket}: {ex.Message}", data, ex);
            }

            if (State == ConnectionState.Closed) throw new DisconnectedException();

            if (packet is HandshakeRequestPacket helloPacket) {
                //TODO: Move this into the Server class

                if (Handshaker == null) throw new InvalidOperationException($"{this}: Can't handle {helloPacket} because there is no {nameof(Handshaker)}");

                HandshakeResult result;
                try {
                    result = await Handshaker.OnHandshakeRequest(helloPacket, this, ClosedCancellationToken);
                } catch (Exception ex) {
                    result = HandshakeResult.Failure("Handshake Failed");

                    Log.Error($"{this}: Handshake Failure: {ex}");
                }

                if (result.Successful) {
                    User = result.User;
                }

                var response = new ResponsePacket(result);

                await Respond(packetId, response, ClosedCancellationToken);

                TransitionState(result.Successful ? ConnectionState.Connected : ConnectionState.Closed);
            } else if (packet is RequestPacket requestPacket) {
                var requestContext = new RequestContext() {
                    Client = Client,
                    Server = Server,
                    Connection = this
                };

                var requestEventHandler = RequestReceived;

                if (requestEventHandler == null)
                    throw new InvalidOperationException($"{this}: Receiving Requests, but nothing is reading them.");

                requestPacket.Context = requestContext;

                var args = new RequestReceivedEventArgs() {
                    Request = requestPacket,
                    Context = requestContext
                };

                foreach (var handler in requestEventHandler.GetInvocationList().Cast<RequestReceived>()) {
                    object result = null;
                    try {
                        result = await handler(this, args);
                    } catch (Exception ex) {
                        Log.Error($"{this}: Error invoking event {nameof(RequestReceived)}: {ex}");
                    }

                    if (result != null) {
                        args.Response = result;
                        args.IsHandled = true;
                    }

                    if (args.IsHandled) break;
                }

                if (requestPacket.Flags.HasFlag(PacketFlag.AckRequired)) {
                    var unhandledRequestResponse = new ResponsePacket(new ErrorResponseData(ErrorResponseCodes.UnhandledRequest, $"Packet {requestPacket} not expected.", false));

                    var response = (!args.IsHandled ? unhandledRequestResponse : new ResponsePacket(args.Response));

                    try {
                        await Respond(packetId, response, ClosedCancellationToken);
                    } catch (Exception ex) {
                        Log.Error($"{this}: Error sending response to Request {requestPacket}: Response={response}: {ex}");
                    }
                }
            } else if (packet is IAck ack) {
                if (_awaitingAck.TryGetValue(ack.PacketId, out var tcs)) {
                    tcs.SetResult(packet);
                } else {
                    Log.Warn($"{this}: Ack: Could not find packet #{ack.PacketId}");
                }
            } else {
                throw new InvalidDataException($"{this}: {packet.GetType().Name} packet not supported.");
            }
        }

        public async Task Respond(ulong requestPacketId, ResponsePacket response, CancellationToken cancellationToken) {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var isCritical = response.Data is ErrorResponseData erd
                && erd.IsCritical;

            if (isCritical)
                Log.Warn($"{this}: Sent critical error {response.Data}: Closing");

            response.PacketId = requestPacketId;

            try {
                await Send(response, cancellationToken);
            } catch (DisconnectedException ex) {
                Log.Warn($"{this}: {nameof(Respond)}: Error sending response packet {response}, disconnected.", ex);
            }

            // If we sent a packet that had a critical error, close the connection.
            if (isCritical)
                throw new DisconnectedException($"{this}: Sent critical error {response.Data}");
        }

        internal int StillAwaitingAck => _awaitingAck.Count;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>> _awaitingAck = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>>();

        #region RPC

        public async Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            if (State == ConnectionState.Closed) throw new InvalidOperationException($"{this}: Connection is closed.");
            var response = await Send(packet, cancellationToken);

            if (packet.Flags.HasFlag(PacketFlag.AckRequired)) {
                if (response == null) return null;

                if (!(response is ResponsePacket responsePacket))
                    throw new InvalidOperationException($"{this}: {nameof(Request)}: Expected a {nameof(ResponsePacket)}, but got a {response?.GetType()?.Name ?? "null"} response");

                responsePacket.Verify();
                return responsePacket;
            }
            return null;
        }

        #endregion

        /// <summary>
        /// Sends data. Returns a <see cref="Task"/>
        /// that completes when the data is sent.
        /// </summary>
        /// <param name="packetId"></param>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task"/>
        /// that completes when the data is sent.</returns>
        /// <exception cref="DisconnectedException">If the connection is closed</exception>
        protected abstract Task SendImpl(ulong packetId, byte[] data, CancellationToken cancellationToken);

        private ulong _nextPacketId = 1;
        private readonly object _idLocker = new object();

        /// <summary>
        /// Sends and configures a <see cref="IPacket"/>.
        /// </summary>
        /// <param name="packet">packet</param>
        /// <param name="cancellationToken">cancellation</param>
        /// <returns><see cref="Task"/> containing an Ack, if one was returned and required.</returns>
        public async Task<IAck> Send(IPacket packet, CancellationToken cancellationToken) {
            //TODO: This should return an IPacket, not an IAck, that way we don't have to deserialize it fully
            // Maybe, and IAck could share whatever required property we need so we can deserialize it
            // like a SerailizedPacket. So possibly merge an IAck with an IPacket
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            if (packet is Packet fullPacket) {
                fullPacket.Sent = DateTimeOffset.Now;
            }

            if (packet is IAck packetAsAck && packetAsAck.PacketId <= 0) {
                throw new InvalidOperationException($"Ack doesn't have response ID.");
            }

            ulong packetId;

            lock (_idLocker)
                packetId = _nextPacketId++;

            TaskCompletionSource<Packet> ackCompletion = null;

            try {
                if (packet.Flags.HasFlag(PacketFlag.AckRequired)) {
                    ackCompletion = new TaskCompletionSource<Packet>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _awaitingAck.AddOrUpdate(packetId, ackCompletion, (_, __) => ackCompletion);
                }

                Log.TracePacketSending(this, packet);

                if (packet.Sent == null)
                    throw new InvalidOperationException($"{this}: Sent is null");

                byte[] packetData = null;

                if (packet is Packet regularPacket) {
                    packetData = SerializedPacket.Create(regularPacket, Serializer)
                        ?? throw new InvalidOperationException($"{this}: Packet serialization returned null");
                } else if (packet is SerializedPacket serializedPacket) {
                    packetData = serializedPacket.Data;
                } else throw new InvalidOperationException($"{this}: Packet of type {packet.GetType().Name} is not supported.");

                try {
                    await SendImpl(packetId, packetData, cancellationToken);
                } catch (DisconnectedException ex) {
                    if (ex.InnerException != null) {
                        Log.Warn($"Disconnected sending packet {packet}: {ex.InnerException}");
                    }
                    TransitionState(ConnectionState.Closed);
                    throw;
                }

                // Don't log this unless the send actually happens
                Log.TracePacketSent(this, packet);

                if (ackCompletion == null) {
                    // No ack required
                    return null;
                } else {
                    // Wait for the ack
                    using (var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ClosedCancellationToken)) {
                        var cancellationTask = Task.Delay(-1, combinedCancellationTokenSource.Token);

                        Log.TraceWaitingForAck(this, packetId);

                        var result = await Task.WhenAny(ackCompletion.Task, Task.Delay(WaitForResponseTimeout), cancellationTask);
                        if (result == ackCompletion.Task) {
                            var ack = (IAck)ackCompletion.Task.Result;
                            Log.TraceAckReceived(this, ack);
                            return ack;
                        }

                        if (result == cancellationTask) {
                            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                            if (ClosedCancellationToken.IsCancellationRequested) throw new NotConnectedException($"{this}: Could not send {packet}, the connection is closed.");
                        }

                        throw new TimeoutException($"{this}: Timed out waiting for Ack from {packet}");
                    }
                }
            } finally {
                if (ackCompletion != null) {
                    ackCompletion.TrySetCanceled();

                    _awaitingAck.TryRemove(packetId, out _);
                }
            }
        }

        public abstract IConnection Clone();

        private readonly CancellationTokenSource _closedCancellation = new CancellationTokenSource();

        protected CancellationToken ClosedCancellationToken {
            get {
                try {
                    return _closedCancellation.Token;
                } catch (ObjectDisposedException) {
                    throw new OperationCanceledException();
                }
            }
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            Log.Info($"{this}:  Dispose({disposing})");

            if (!_disposedValue) {
                if (disposing) {
                    _closedCancellation.Cancel();

                    foreach (RequestReceived callback in (RequestReceived?.GetInvocationList() ?? Enumerable.Empty<Delegate>())) {
                        RequestReceived -= callback;
                    }

                    _closedCancellation?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
