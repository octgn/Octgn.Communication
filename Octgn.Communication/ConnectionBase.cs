using System;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using Octgn.Communication.Utility;
using System.Collections.Generic;

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

        protected ConnectionBase(string remoteAddress, IHandshaker handshaker) {
            if (string.IsNullOrWhiteSpace(remoteAddress)) throw new ArgumentNullException(remoteAddress);

            Handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));

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

        public Server Server { get;  private set; }
        public virtual void Initialize(Server server) {
            Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public Client Client { get; private set; }
        public virtual void Initialize(Client client) {
            Client = client ?? throw new ArgumentNullException(nameof(client));
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

            if(stateTransitioned)
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

                TransitionState(ConnectionState.Handshaking);

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

        protected abstract Task ConnectImpl(CancellationToken cancellationToken);

        public void Close(){
            TransitionState(ConnectionState.Closed);
        }

        public event RequestReceived RequestReceived;

        protected Task ProcessReceivedPackets(IEnumerable<Packet> packets) {
            return Task.WhenAll(packets.Select(ProcessReceivedPacket));
        }

        protected async Task ProcessReceivedPacket(Packet packet) {
            try {
                Log.TracePacketReceived(this, packet);

                if (State == ConnectionState.Closed) {
                    Log.Warn($"{this}: Connection is closed. Dropping packet {packet}");
                    return;
                }

                if(packet is RequestPacket rp) {
                    rp.Context = new RequestContext() {
                        Client = Client,
                        Server = Server,
                        Connection = this
                    };
                }

                switch (packet) {
                    case HandshakeRequestPacket helloPacket: {
                            if (Handshaker == null) throw new InvalidOperationException($"{this}: Can't handle {helloPacket} because there is no {nameof(Handshaker)}");

                            var result = await Handshaker.OnHandshakeRequest(helloPacket, this, ClosedCancellationToken);

                            if (result.Successful) {
                                User = result.User;
                            }

                            var response = new ResponsePacket(helloPacket, result);

                            await Respond(helloPacket, response, ClosedCancellationToken);

                            TransitionState(result.Successful ? ConnectionState.Connected : ConnectionState.Closed);

                            break;
                        }
                    case RequestPacket requestPacket: {
                            var requestEventHandler = RequestReceived;

                            if (requestEventHandler == null)
                                throw new InvalidOperationException($"{this}: Receiving Requests, but nothing is reading them.");

                            var args = new RequestReceivedEventArgs() {
                                Request = requestPacket,
                                Context = new RequestContext {
                                    Connection = this
                                }
                            };

                            foreach (var handler in requestEventHandler.GetInvocationList().Cast<RequestReceived>()) {
                                var result = await handler(this, args);
                                if (result != null) {
                                    args.Response = result;
                                    args.IsHandled = true;
                                }
                                if (args.IsHandled) break;
                            }

                            // No response required.
                            if (!args.IsHandled && !requestPacket.RequiresAck) break;

                            var unhandledRequestResponse = new ResponsePacket(requestPacket, new ErrorResponseData(ErrorResponseCodes.UnhandledRequest, $"Packet {requestPacket} not expected.", false));

                            var response = args.Response
                                ?? (!args.IsHandled ? unhandledRequestResponse : new ResponsePacket(requestPacket, null));

                            await Respond(requestPacket, response, ClosedCancellationToken);

                            break;
                        }
                    case IAck ack: {
                            if (_awaitingAck.TryGetValue(ack.PacketId, out var tcs))
                                tcs.SetResult(packet);
                            else
                                throw new InvalidOperationException($"{this}: Ack: Could not find packet #{ack.PacketId}");
                            break;
                        }
                    default:
#pragma warning disable RCS1079 // Throwing of new NotImplementedException.
                        throw new NotImplementedException($"{this}: {packet.GetType().Name} packet not supported.");
#pragma warning restore RCS1079 // Throwing of new NotImplementedException.
                }
            } catch (Exception ex) {
                Signal.Exception(ex, $"{this}: Error processing packet {packet}");
            }
        }

        private async Task Respond(RequestPacket request, ResponsePacket response, CancellationToken cancellationToken) {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var isCritical = response.Data is ErrorResponseData erd
                && erd.IsCritical;

            if (isCritical)
                Log.Warn($"{this}: Sent critical error {response.Data}: Closing");

            StampPacketBeforeSend(response);

            response.PacketId = (ulong)request.Id;

            try {
                await Send(response, cancellationToken);
            } catch (DisconnectedException ex) {
                Log.Warn($"{this}: {nameof(ProcessReceivedPacket)}: Error sending response packet {response}, disconnected.", ex);
            }

            // If we sent a packet that had a critical error, close the connection.
            if (isCritical)
                throw new InvalidOperationException($"{this}: Sent critical error {response.Data}");
        }

        internal int StillAwaitingAck => _awaitingAck.Count;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>> _awaitingAck = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>>();

        #region RPC

        public async Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            if (State == ConnectionState.Closed) throw new InvalidOperationException($"{this}: Connection is closed.");
            StampPacketBeforeSend(packet);
            var response = await Send(packet, cancellationToken);

            if (response == null && !packet.RequiresAck) return null;

            if (!(response is ResponsePacket responsePacket))
                throw new InvalidOperationException($"{this}: {nameof(Request)}: Expected a {nameof(ResponsePacket)}, but got a {response?.GetType()?.Name ?? "null"} response");

            responsePacket.Verify();
            return responsePacket;
        }

        #endregion

        private ulong _nextPacketId;
        private void StampPacketBeforeSend(Packet packet) {
            packet.Id = _nextPacketId++;
            packet.Sent = DateTimeOffset.Now;
        }

        /// <summary>
        /// Sends a <see cref="Packet"/>. Returns a <see cref="Task"/>
        /// that completes when the <see cref="Packet"/> is sent.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task"/>
        /// that completes when the <see cref="Packet"/> is sent.</returns>
        protected abstract Task SendImpl(Packet packet, CancellationToken cancellationToken);

        /// <summary>
        /// Sends and configures a <see cref="Packet"/>.
        /// Returns a <see cref="Task<IAck>"/> that completes when
        /// the packet is sent and an ack is receieved (if one was required: see <see cref="Packet"/>.<see cref="Packet.RequiresAck"/>)
        /// </summary>
        /// <param name="packet">packet</param>
        /// <param name="cancellationToken">cancellation</param>
        /// <returns><see cref="Task"/> containing an Ack, if one was returned and required.</returns>
        protected async Task<IAck> Send(Packet packet, CancellationToken cancellationToken) {
            if (packet?.Id == null) throw new ArgumentNullException(nameof(packet), nameof(packet) + " or packet.Id is null");
            // Just incase this value changes, we want to be able to look it up in our dictionary for sure in the finally block
            var packetId = packet.Id.Value;

            if (!packet.RequiresAck) {
                Log.TracePacketSending(this, packet);

                try {
                    await SendImpl(packet, cancellationToken);
                } catch (DisconnectedException ex) {
                    if(ex.InnerException != null) {
                        Log.Warn($"Disconnected sending packet {packet}: {ex.InnerException}");
                    }
                    TransitionState(ConnectionState.Closed);
                    throw;
                }

                Log.TracePacketSent(this, packet);

                return null;
            }

            // Wait for the ack
            var tcs = new TaskCompletionSource<Packet>(TaskCreationOptions.RunContinuationsAsynchronously);

            try {
                _awaitingAck.AddOrUpdate(packetId, tcs, (_, __) => tcs);

                Log.TracePacketSending(this, packet);

                try {
                    await SendImpl(packet, cancellationToken);
                } catch (DisconnectedException ex) {
                    if(ex.InnerException != null) {
                        Log.Warn($"Disconnected sending packet {packet}: {ex.InnerException}");
                    }
                    TransitionState(ConnectionState.Closed);
                    throw;
                }

                // Don't log this unless the send actually happens
                Log.TracePacketSent(this, packet);

                // Combine cancellations
                // Then create a  monitor task

                using (var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ClosedCancellationToken)) {
                    var cancellationTask = Task.Delay(-1, combinedCancellationTokenSource.Token);

                    Log.TraceWaitingForAck(this, packetId);

                    var result = await Task.WhenAny(tcs.Task, Task.Delay(WaitForResponseTimeout), cancellationTask);
                    if (result == tcs.Task) {
                        var ack = (IAck)tcs.Task.Result;
                        Log.TraceAckReceived(this, ack);
                        return ack;
                    }

                    if (result == cancellationTask) {
                        if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                        if (ClosedCancellationToken.IsCancellationRequested) throw new NotConnectedException($"{this}: Could not send {packet}, the connection is closed.");
                    }

                    throw new TimeoutException($"{this}: Timed out waiting for Ack from {packet}");
                }
            } finally {
                // Just in case it wasn't set.
                tcs.TrySetCanceled();

                _awaitingAck.TryRemove(packetId, out tcs);
            }
        }

        public abstract IConnection Clone();

        private readonly CancellationTokenSource _closedCancellation = new CancellationTokenSource();

        protected CancellationToken ClosedCancellationToken => _closedCancellation.Token;

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
