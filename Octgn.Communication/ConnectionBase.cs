﻿using System;
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
        public static TimeSpan WaitToConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

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

        public virtual async Task Connect(CancellationToken cancellationToken = default(CancellationToken)) {
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

#pragma warning disable RCS1159 // Use EventHandler<T>.
        // RequestReceived returns a Task, and we need that.
        private event RequestReceived _requestReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.

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

        protected async Task ProcessReceivedPacket(Packet packet, CancellationToken cancellationToken) {
            async Task Respond(ResponsePacket response) {
                if (response == null) throw new ArgumentNullException(nameof(response));

                var isCritical = response.Data is ErrorResponseData erd
                    && erd.IsCritical;

                if (isCritical)
                    Log.Warn($"{this}: Sent critical error {response.Data}: Closing");

                StampPacketBeforeSend(response);

                try {
                    await SendPacket(response, cancellationToken);
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

                    // No response required.
                    if (!args.IsHandled && !requestPacket.RequiresAck) break;

                    var unhandledRequestResponse = new ResponsePacket(requestPacket, new ErrorResponseData(ErrorResponseCodes.UnhandledRequest, $"Packet {requestPacket} not expected.", false));

                    var response = args.Response
                        ?? (!args.IsHandled ? unhandledRequestResponse : new ResponsePacket(requestPacket, null));

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

        internal int StillAwaitingAck => _awaitingAck.Count;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>> _awaitingAck = new System.Collections.Concurrent.ConcurrentDictionary<ulong, TaskCompletionSource<Packet>>();

        #region RPC

        public async Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            if (IsClosed) throw new InvalidOperationException($"{this}: Connection is closed.");
            StampPacketBeforeSend(packet);
            var response = await SendPacket(packet, cancellationToken);

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

        protected async Task<Packet> SendPacket(Packet packet, CancellationToken cancellationToken) {
            if (packet?.Id == null) throw new ArgumentNullException(nameof(packet), nameof(packet) + " or packet.Id is null");

            if (!packet.RequiresAck) {
                Log.TracePacketSent(this, packet);

                await SendPacketImplementation(packet, cancellationToken);

                return null;
            }

            // Wait for the ack
            var tcs = new TaskCompletionSource<Packet>(TaskCreationOptions.RunContinuationsAsynchronously);
            try {
                _awaitingAck.AddOrUpdate(packet.Id.Value, tcs, (a, b) => tcs);

                Log.TracePacketSent(this, packet);

                await SendPacketImplementation(packet, cancellationToken);

                using (var sendPacketCancellationTokenSource = new CancellationTokenTaskSource<object>(cancellationToken)) {
#if (DEBUG)
                    Log.Info($"{this}: Waiting for ack for #{packet.Id}");
#endif
                    var result = await Task.WhenAny(tcs.Task, ClosedCancellationTask, Task.Delay(WaitForResponseTimeout), sendPacketCancellationTokenSource.Task);
                    if (result == tcs.Task) {
#if (DEBUG)
                        Log.Info($"Ack for #{packet.Id} received");
#endif
                        return tcs.Task.Result;
                    }

                    if (result == ClosedCancellationTask) throw new NotConnectedException($"{this}: Could not send {packet}, the connection is closed.");

                    if (result == sendPacketCancellationTokenSource.Task) throw new OperationCanceledException(cancellationToken);

                    throw new TimeoutException($"{this}: Timed out waiting for Ack from {packet}");
                }
            } finally {
                Log.Info($"{this}: Finished waiting for Ack for #{packet.Id}");

                // Just in case it wasn't set.
                tcs.TrySetCanceled();

                _awaitingAck.TryRemove(packet.Id.Value, out tcs);
            }
        }

        protected abstract Task SendPacketImplementation(Packet packet, CancellationToken cancellationToken);

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

        protected CancellationToken ClosedCancellationToken =>  _closedCancellation.Token;
        protected Task ClosedCancellationTask { get; }

        public ISerializer Serializer { get; set; }

        protected ConnectionBase() {
            _closedCancellationTaskSource = new CancellationTokenTaskSource<object>(_closedCancellation.Token);
            ClosedCancellationTask = _closedCancellationTaskSource.Task;
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

            Serializer = null;

            if (timedOut)
                throw new InvalidOperationException($"{this}: Timed out waiting for the read loop to end.");

            _closedCancellationTaskSource?.Dispose();

            foreach (RequestReceived callback in (_requestReceived?.GetInvocationList() ?? Enumerable.Empty<Delegate>())) {
                _requestReceived -= callback;
            }
        }
    }
}
