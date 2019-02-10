using Octgn.Communication.Packets;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules
{
    public class StatsModule : Module
    {
        private static readonly ILogger Log = LoggerFactory.Create(nameof(StatsModule));

        public const string OnlineUserPacketKey = "OnlineUserCount";

        public Stats Stats {
            get => _stats;
            private set {
                if (value == _stats) return;
                _stats = value;
            }
        }

        /// <summary>
        /// The amount of time between sending all the connected clients the <see cref="Stats"/>
        /// </summary>
        public TimeSpan UpdateClientsInterval { get; set; } = TimeSpan.FromSeconds(30);

        private Stats _stats;
        public event EventHandler<StatsModuleUpdateEventArgs> StatsModuleUpdate;

        private readonly Server _server;
        private readonly Client _client;
        public StatsModule(Server server) {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public override void Initialize() {
            if (_server != null) {
                SendStatsToUsers().SignalOnException();
            }
            base.Initialize();
        }

        public StatsModule(Client client) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stats = new Stats() {
                OnlineUserCount = 0,
                Date = DateTime.Now
            };
        }

        public override async Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default) {
            if (obj is Stats stats) {

                Stats = stats;

                StatsModuleUpdate?.Invoke(this, new StatsModuleUpdateEventArgs() {
                    Stats = Stats
                });

                return ProcessResult.Processed;
            }

            return await base.Process(obj, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendStatsToUsers() {
            if (IsDisposed) {
                Log.Info($"Disposed. Stopping {nameof(SendStatsToUsers)}");
                return;
            }

            try {
                await Task.Delay(UpdateClientsInterval, _disposedCancellationTokenSource.Token);
            } catch (TaskCanceledException) {
                Log.Info($"Disposed. Stopping {nameof(SendStatsToUsers)}");
                return;
            }

            if (IsDisposed) {
                Log.Info($"Disposed. Stopping {nameof(SendStatsToUsers)}");
                return;
            }

            var onlineUsers = _server.ConnectionProvider
                .GetConnections()
                .Count(con => con.State == ConnectionState.Connected);

            _stats = new Stats() {
                Date = DateTimeOffset.Now,
                OnlineUserCount = onlineUsers
            };

            StatsModuleUpdate?.Invoke(this, new StatsModuleUpdateEventArgs() {
                Stats = _stats
            });

            try {
                await _server.Request(new Stats(_stats));
            } catch (ErrorResponseException ex) when (ex.Code == ErrorResponseCodes.UserOffline) {
                // Don't care.
                // This happens when there are no users online
                Log.Warn(nameof(SendStatsToUsers), ex);
            }

            if (IsDisposed) {
                Log.Info($"Disposed. Stopping {nameof(SendStatsToUsers)}");
                return;
            }

            SendStatsToUsers().SignalOnException();
        }

        private readonly CancellationTokenSource _disposedCancellationTokenSource = new CancellationTokenSource();

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _disposedCancellationTokenSource.Cancel();
                _disposedCancellationTokenSource.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class StatsModuleUpdateEventArgs : EventArgs
    {
        public Stats Stats { get; set; }
    }

    public class Stats : RequestPacket
    {
        static Stats() {
            RegisterPacketType<Stats>();
        }

        public DateTimeOffset Date {
            get => DateTimeOffset.Parse((string)this[GetName()]);
            set => this[GetName()] = value.ToString("o");
        }

        public int OnlineUserCount {
            get => (int)this[GetName()];
            set => this[GetName()] = value;
        }

        public Stats() : base(nameof(Stats)) {

        }

        public Stats(Stats stats) : this() {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            this.Date = stats.Date;
            this.OnlineUserCount = stats.OnlineUserCount;
        }

        private static string GetName([System.Runtime.CompilerServices.CallerMemberName]string name = null) {
            return nameof(Stats) + ":" + name;
        }

        public override byte PacketType => 7;

        public override PacketFlag Flags => PacketFlag.None;


        protected override string PacketStringData => $"INFO: UOC={OnlineUserCount}";
    }
}
