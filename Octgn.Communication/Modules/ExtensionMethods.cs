using System;

namespace Octgn.Communication.Modules
{
    public static class ExtensionMethods
    {
        public static StatsModule InitializeStatsModule(this Client client) {
            var module = new StatsModule(client);
            client.Attach(module);
            return module;
        }

        public static StatsModule InitializeStatsModule(this Server server) {
            var module = new StatsModule(server);
            server.Attach(module);
            return module;
        }

        public static StatsModule Stats(this Client client) {
            return client.GetModule<StatsModule>();
        }

        public static StatsModule Stats(this Server server) {
            return server.GetModule<StatsModule>();
        }
    }
}
