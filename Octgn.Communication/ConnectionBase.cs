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
        private static ILogger Log = LoggerFactory.Create(nameof(ConnectionBase));
        public static TimeSpan WaitForResponseTimeout { get; set; } = TimeSpan.FromSeconds(15);

        #region Identification
        public string ConnectionId { get; } = UID.Generate(++_nextSeed);

        private static int _nextSeed = 0;

        public bool Equals(IConnection other)
        {
            if (other == null || this == null) return false;
            return other.ConnectionId == this.ConnectionId;
        }
        #endregion

        public abstract bool IsConnected { get; }

        private bool _startedReadingPackets;
        private bool _calledConnect;
        private bool _calledRequestReceived;
        private readonly object L_STARTEDREADINGPACKETS = new object();

        public virtual async Task Connect()
        {
            await Task.Run(() => {
                lock (L_STARTEDREADINGPACKETS) {
                    _calledConnect = true;
                    if (!_calledRequestReceived) return;

                    if (!_startedReadingPackets) {
                        _startedReadingPackets = true;
                        StartReadingPackets();
                    }
                }
            });
        }

        event RequestPacketReceived IConnection.RequestReceived {
            add {
                _requestReceived += value;
                lock (L_STARTEDREADINGPACKETS) {
                    _calledRequestReceived = true;
                    if (!_calledConnect && !IsConnected) return;

                    if (!_startedReadingPackets) {
                        _startedReadingPackets = true;
                        StartReadingPackets();
                    }
                }
            }
            remove => throw new InvalidOperationException("This event cleans itself up");
        }

        private event RequestPacketReceived _requestReceived;

        protected void StartReadingPackets() {
            Log.Info($"{this}: {nameof(StartReadingPackets)}");
            Task.Run(ReadPacketsAsync);
        }

        protected abstract Task ReadPacketsAsync();

        protected async Task FirePacketReceived(Packet packet)
        {
            Log.TracePacketReceived(this, packet);

            var ack = new Ack(packet);

            if (packet is Ack) {
                ack = null;
                var ackPacket = packet as Ack;
                if (_awaitingAck.TryGetValue(ackPacket.PacketId, out TaskCompletionSource<Ack> tcs)) {
                    tcs.SetResult(ackPacket);
                } else Log.Warn($"{this}: Ack: Could not find packet #{ackPacket.PacketId}");
            } else if (packet is RequestPacket) {
                if (_requestReceived == null)
                    throw new InvalidOperationException("Receiving Requests, but nothing is reading them.");

                _requestReceived?.Invoke(this, new RequestPacketReceivedEventArgs {
                    Connection = this,
                    Packet = packet as RequestPacket
                });
            } else if (packet is ResponsePacket) {
                var resp = packet as ResponsePacket;
                if (_awaitingResponse.TryGetValue(resp.InResponseTo, out TaskCompletionSource<ResponsePacket> tcs)) {
                    tcs.SetResult(resp);
                } else Log.Warn($"{this}: Response: Could not find packet #{resp.InResponseTo}");
            } else throw new NotImplementedException($"{packet.GetType().Name} packet not supported.");

            if (ack != null) {
                StampPacketBeforeSend(ack);
                await SendPacket(ack);
            }
        }


        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Ack>> _awaitingAck = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Ack>>();

        #region RPC

        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<ResponsePacket>> _awaitingResponse = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<ResponsePacket>>();
        public async Task<ResponsePacket> Request(RequestPacket packet)
        {
            if (IsClosed) throw new InvalidOperationException("Connection is closed.");
            TaskCompletionSource<ResponsePacket> tcs = null;
            try {
                tcs = new TaskCompletionSource<ResponsePacket>();
                StampPacketBeforeSend(packet);
                _awaitingResponse.AddOrUpdate(packet.Id.Value, tcs, (a, b) => tcs);
                await SendPacket(packet);

                var response = tcs.Task.Result;
                response.Verify();
                return response;

            } finally {
                Log.Info($"Finished waiting for Response for #{packet.Id}");
                _awaitingResponse.TryRemove(packet.Id.Value, out TaskCompletionSource<ResponsePacket> removedWait);
            }
        }

        public async Task Response(ResponsePacket packet)
        {
            if (IsClosed) throw new InvalidOperationException("Connection is closed.");
            StampPacketBeforeSend(packet);
            await SendPacket(packet);
        }

        #endregion

        private ulong _nextPacketId;
        private void StampPacketBeforeSend(Packet packet)
        {
            packet.Id = _nextPacketId++;
            packet.Sent = DateTimeOffset.Now;
        }

        protected virtual async Task SendPacket(Packet packet)
        {
            if (packet?.Id == null) throw new ArgumentNullException(nameof(packet), nameof(packet) + " or packet.Id is null");

            Log.TracePacketSent(this, packet);
            if (packet is Ack) return;

            var tcs = new TaskCompletionSource<Ack>();
            try {
                _awaitingAck.AddOrUpdate(packet.Id.Value, tcs, (a, b) => tcs);
#if(DEBUG)
                Log.Info($"Waiting for ack for #{packet.Id}");
#endif
                var result = await Task.WhenAny(tcs.Task, _closedCancellationTask, Task.Delay(WaitForResponseTimeout));
                if (result == _closedCancellationTask) throw new NotConnectedException($"Could not send {packet}, the connection is closed.");
                else if (result == tcs.Task) {
#if(DEBUG)
                    Log.Info($"Ack for #{packet.Id} received");
#endif
                    return;
                } else throw new TimeoutException($"Timed out waiting for Ack from {packet}");
            } finally {
                Log.Info($"Finished waiting for Ack for #{packet.Id}");
                _awaitingAck.TryRemove(packet.Id.Value, out tcs);
                tcs?.TrySetException(new Exception("Set from finally in ConnectionBase.Request"));
            }
        }

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

        public event ConnectionClosed ConnectionClosed;

        private CancellationTokenSource _closedCancellation = new CancellationTokenSource();
        private CancellationTokenTaskSource<object> _closedCancellationTaskSource;
        private Task _closedCancellationTask;

        public ISerializer Serializer { get; set; }

        public ConnectionBase() {
            _closedCancellationTaskSource = new CancellationTokenTaskSource<object>(_closedCancellation.Token);
            _closedCancellationTask = _closedCancellationTaskSource.Task;
        }

        protected virtual void Close(ConnectionClosedEventArgs args)
        {
            _closedCancellation.Cancel();
            if (_awaitingResponse.Count > 0) {
                foreach(var item in _awaitingResponse) {
                    item.Value.TrySetCanceled();
                }
                Log.Warn($"{this}: {_awaitingResponse.Count} responses never received.");
                _awaitingResponse.Clear();
            }
            if (_awaitingAck.Count > 0) {
                foreach (var item in _awaitingAck) {
                    item.Value.TrySetCanceled();
                }
                Log.Warn($"{this}: {_awaitingAck.Count} acks never received.");

                _awaitingAck.Clear();
            }
        }

        public virtual void Dispose()
        {
            Log.Info($"{this}:  Dispose");
            _closedCancellationTaskSource?.Dispose();
            if (_awaitingResponse.Count > 0) {
                foreach(var item in _awaitingResponse) {
                    item.Value.TrySetCanceled();
                }
                Log.Warn($"{this}: {_awaitingResponse.Count} responses never received.");
                _awaitingResponse.Clear();
            }
            if (_awaitingAck.Count > 0) {
                foreach (var item in _awaitingAck) {
                    item.Value.TrySetCanceled();
                }
                Log.Warn($"{this}: {_awaitingAck.Count} acks never received.");

                _awaitingAck.Clear();
            }
#pragma warning disable IDE0007 // Use implicit type
            foreach (RequestPacketReceived callback in (_requestReceived?.GetInvocationList() ?? Enumerable.Empty<Delegate>())) {
#pragma warning restore IDE0007 // Use implicit type
                _requestReceived -= callback;
            }
        }
    }
}
