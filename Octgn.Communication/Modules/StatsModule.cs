using Octgn.Communication.Packets;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules
{
    public class StatsModule : IServerModule, IClientModule
    {
        public const string OnlineUserPacketKey = "OnlineUserCount";

        public Stats Stats {
            get => _stats;
            private set {
                if (value == _stats) return;
                _stats = value;
            }
        }
        private Stats _stats;
        public event EventHandler<StatsModuleUpdateEventArgs> StatsModuleUpdate;

        private readonly Server _server;
        public StatsModule(Server server) {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public StatsModule() {
            _stats = new Stats() {
                OnlineUserCount = 0,
                Date = DateTime.Now
            };
        }

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            // We only want to process client side requests
            if (args.Context.Client == null) return Task.CompletedTask;

            // Not the type of request we're looking for
            if (args.Request.Name != nameof(Stats)) return Task.CompletedTask;

            Stats = (Stats)args.Request;

            StatsModuleUpdate?.Invoke(this, new StatsModuleUpdateEventArgs() {
                Stats = Stats
            });

            return Task.CompletedTask;
        }

        public async Task UserStatusChanged(object sender, UserStatusChangedEventArgs e) {
            // Will not scale well
            // Called on the server side
            var stats = _stats;
            var onlineUsers = _server.ConnectionProvider.GetConnections().Count();
            _stats = new Stats() {
                Date = DateTimeOffset.Now,
                OnlineUserCount = onlineUsers
            };

            await _server.Request(new Stats(_stats));
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

        public override byte PacketTypeId => 7;

        public override bool RequiresAck => false;

        protected override string PacketStringData => $"INFO";
    }
}
