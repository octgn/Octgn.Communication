using System;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using Nito.AsyncEx;
using System.Linq;
using Octgn.Communication.Utility;

namespace Octgn.Communication
{
    public abstract class ConnectionBase : IConnection, IDisposable
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(ConnectionBase));
#pragma warning restore IDE1006 // Naming Styles
        public static TimeSpan WaitForResponseTimeout { get; set; } = TimeSpan.FromSeconds(15);

        #region Identification
        public string ConnectionId { get; } = UID.Generate(++_nextSeed);

        private static int _nextSeed = 0;

        public bool Equals(IConnection other) {
            if (other == null || this == null) return false;
            return other.ConnectionId == this.ConnectionId;
        }
        #endregion

        public abstract bool IsConnected { get; }

        private bool _startedReadingPackets;
        private bool _calledConnect;
        private bool _calledRequestReceived;
        private readonly object _startedReadingPacketsLock = new object();

        public virtual async Task Connect() {
            await Task.Run(() => {
                lock (_startedReadingPacketsLock) {
                    _calledConnect = true;
                    if (!_calledRequestReceived) return;

                    if (!_startedReadingPackets) {
                        _startedReadingPackets = true;
                        StartReadingPackets();
                    }
                }
            });
        }

        public event RequestReceived RequestReceived {
            add {
                _requestReceived += value;
                lock (_startedReadingPacketsLock) {
                    _calledRequestReceived = true;
                    if (!_calledConnect && !IsConnected) return;

                    if (!_startedReadingPackets) {
                        _startedReadingPackets = true;
                        StartReadingPackets();
                    }
                }
            }
            remove => _requestReceived -= value;
        }

        private event RequestReceived _requestReceived;

        private Task _readPacketsTask;

        protected async void StartReadingPackets() {
            Log.Info($"{this}: {nameof(StartReadingPackets)}");
            if (_readPacketsTask != null) throw new InvalidOperationException();

            try {
                await (_readPacketsTask = ReadPacketsAsync());
            } catch (DisconnectedException ex) {
                Log.Warn($"{this}: Disconnected", ex);
            } catch (Exception ex) {
                Signal.Exception(ex);
            } finally {
                Log.Info($"{this}: {nameof(StartReadingPackets)}: Complete");
            }

            IsClosed = true;
        }

        protected abstract Task ReadPacketsAsync();

        protected async Task ProcessReceivedPacket(Packet packet) {
            async Task Respond(ResponsePacket response) {
                if (response == null) throw new ArgumentNullException(nameof(response));

                var isCritical = response.Data is ErrorResponseData erd
                    && erd.IsCritical;

                if (isCritical)
                    Log.Warn($"{this}: Sent critical error {response.Data}: Closing");

                StampPacketBeforeSend(response);

                try {
                    await SendPacket(response);
                } catch (DisconnectedException ex) {
                    Log.Warn($"{this}: {nameof(ProcessReceivedPacket)}: Error sending response packet {response}, disconnected.", ex);
                }

                // If we sent a packet that had a critical error, close the connection.
                if (isCritical)
                    throw new InvalidOperationException($"{this}: Sent critical error {response.Data}");
            }

            Log.TracePacketReceived(this, packet);

            switch (packet) {
                case RequestPacket requestPacket:
                    if (_requestReceived == null)
                        throw new InvalidOperationException($"{this}: Receiving Requests, but nothing is reading them.");

                    var args = new RequestReceivedEventArgs() {
                        Request = requestPacket,
                        Context = new RequestContext {
                            Connection = this
                        }
                    };

                    var eventTask = _requestReceived?.Invoke(this, args);
                    await eventTask;
                    if(eventTask is Task<ResponsePacket> responseTask) {
                        args.Response = responseTask.Result;
                        args.IsHandled = args.Response != null;
                    }

                    var unhandledRequestResponse = new ResponsePacket(args.Request, new ErrorResponseData(ErrorResponseCodes.UnhandledRequest, $"Packet {args.Request} not expected.", false));

                    var response = args.Response
                        ?? (!args.IsHandled ? unhandledRequestResponse : new ResponsePacket(args.Request, null));

                    await Respond(response);

                    break;

                case IAck ack:
                    if (_awaitingAck.TryGetValue(ack.PacketId, out TaskCompletionSource<Packet> tcs))
                        tcs.SetResult(packet);
                    else
                        throw new InvalidOperationException($"{this}: Ack: Could not find packet #{ack.PacketId}");
                    break;

                default:
#pragma warning disable RCS1079 // Throwing of new NotImplementedException.
                    throw new NotImplementedException($"{this}: {packet.GetType().Name} packet not supported.");
#pragma warning restore RCS1079 // Throwing of new NotImplementedException.
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>> _awaitingAck = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>>();

        #region RPC

        public async Task<ResponsePacket> Request(RequestPacket packet) {
            if (IsClosed) throw new InvalidOperationException($"{this}: Connection is closed.");
            StampPacketBeforeSend(packet);
            var response = await SendPacket(packet);

            if (!(response is ResponsePacket responsePacket))
                throw new InvalidOperationException($"{this}: {nameof(Request)}: Expected a {nameof(ResponsePacket)}, but got a {response.GetType().Name}");

            responsePacket.Verify();
            return responsePacket;
        }

        #endregion

        private ulong _nextPacketId;
        private void StampPacketBeforeSend(Packet packet) {
            packet.Id = _nextPacketId++;
            packet.Sent = DateTimeOffset.Now;
        }

        protected async Task<Packet> SendPacket(Packet packet) {
            if (packet?.Id == null) throw new ArgumentNullException(nameof(packet), nameof(packet) + " or packet.Id is null");

            if (!packet.RequiresAck) {

                Log.TracePacketSent(this, packet);

                await SendPacketImplementation(packet);

                return null;
            }

            // Wait for the ack
            var tcs = new TaskCompletionSource<Packet>();
            try {
                _awaitingAck.AddOrUpdate(packet.Id.Value, tcs, (a, b) => tcs);

                Log.TracePacketSent(this, packet);

                await SendPacketImplementation(packet);

#if(DEBUG)
                Log.Info($"{this}: Waiting for ack for #{packet.Id}");
#endif
                var result = await Task.WhenAny(tcs.Task, _closedCancellationTask, Task.Delay(WaitForResponseTimeout));
                if (result == tcs.Task) {
#if(DEBUG)
                    Log.Info($"Ack for #{packet.Id} received");
#endif
                    return tcs.Task.Result;
                }

                if (result == _closedCancellationTask) throw new NotConnectedException($"{this}: Could not send {packet}, the connection is closed.");

                throw new TimeoutException($"{this}: Timed out waiting for Ack from {packet}");
            } finally {
                Log.Info($"{this}: Finished waiting for Ack for #{packet.Id}");

                // Just in case it wasn't set.
                tcs.TrySetCanceled();

                _awaitingAck.TryRemove(packet.Id.Value, out tcs);
            }
        }

        protected abstract Task SendPacketImplementation(Packet packet);

        public abstract IConnection Clone();

        public bool IsClosed {
            get => _isClosedCounter != 0;
            set {
                if (Interlocked.CompareExchange(ref _isClosedCounter, 1, 0) != 0) return;
                Log.Info($"{this} Closed");
                var args = new ConnectionClosedEventArgs { Connection = this };
                try {
                    Close(args);
                } catch (Exception ex) {
                    args.Exception = ex;
                }
                ConnectionClosed?.Invoke(this, args);
            }
        }

        private int _isClosedCounter = 0;

        public event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;

        private readonly CancellationTokenSource _closedCancellation = new CancellationTokenSource();
        private readonly CancellationTokenTaskSource<object> _closedCancellationTaskSource;
        private readonly Task _closedCancellationTask;

        protected CancellationToken ClosedCancellationToken =>  _closedCancellation.Token;
        protected Task ClosedCancellationTask => _closedCancellationTask;

        public ISerializer Serializer { get; set; }

        protected ConnectionBase() {
            _closedCancellationTaskSource = new CancellationTokenTaskSource<object>(_closedCancellation.Token);
            _closedCancellationTask = _closedCancellationTaskSource.Task;
        }

        protected virtual void Close(ConnectionClosedEventArgs args) {
            Log.Info($"{this}:  Close");
            Dispose();
        }

        public virtual void Dispose() {
            Log.Info($"{this}:  Dispose");
            _closedCancellation.Cancel();

            var timedOut = false;
            try {
                timedOut = _readPacketsTask?.Wait(ConnectionBase.WaitForResponseTimeout) == false;
            } catch { /* this exception is caught above */ }

            if (timedOut)
                throw new InvalidOperationException($"{this}: Timed out waiting for the read loop to end.");

            _closedCancellationTaskSource?.Dispose();

#pragma warning disable IDE0007 // Use implicit type
            foreach (RequestReceived callback in (_requestReceived?.GetInvocationList() ?? Enumerable.Empty<Delegate>())) {
#pragma warning restore IDE0007 // Use implicit type
                _requestReceived -= callback;
            }
        }
    }
}
