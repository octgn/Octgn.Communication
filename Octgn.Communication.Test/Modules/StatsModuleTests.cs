using NUnit.Framework;
using Octgn.Communication.Modules;
using Octgn.Communication.Serializers;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test.Modules
{
    [TestFixture]
    [NonParallelizable]
    [Parallelizable(ParallelScope.None)]
    public class StatsModuleTests : TestBase
    {
        [TestCase]
        public async Task Implementation() {
            var port = NextPort;

            using (var eveStatsUpdateOnServer = new AutoResetEvent(false)) {

                using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new TestHandshaker()), new InMemoryConnectionProvider(), new XmlSerializer())) {
                    server.Attach(new StatsModule(server));

                    // We want the stats module to ticks faster to get this test over with quicker.
                    server.GetModule<StatsModule>().UpdateClientsInterval = TimeSpan.FromSeconds(1);
                    server.GetModule<StatsModule>().StatsModuleUpdate += (sender, args) => eveStatsUpdateOnServer.Set();

                    server.Initialize();

                    using (var clientA = CreateClient(port, "clientA"))
                    using (var clientB = CreateClient(port,"clientB")) {
                        clientA.Attach(new StatsModule(clientA));
                        clientB.Attach(new StatsModule(clientB));

                        using (var eveStatsReceived = new AutoResetEvent(false)) {
                            clientA.Stats().StatsModuleUpdate += (sender, args) => eveStatsReceived.Set();

                            await clientA.Connect("localhost");

                            Assert.True(eveStatsReceived.WaitOne(10000), "Clients stats module never updated.");

                            Assert.NotNull(clientA.Stats().Stats);
                            Assert.AreEqual(1, clientA.Stats().Stats.OnlineUserCount);

                            await clientB.Connect("localhost");

                            Assert.True(eveStatsReceived.WaitOne(10000));

                            Assert.AreEqual(2, clientA.Stats().Stats.OnlineUserCount);

                            clientB.Dispose();

                            Assert.True(eveStatsReceived.WaitOne(10000));

                            Assert.AreEqual(1, clientA.Stats().Stats.OnlineUserCount);

                            eveStatsUpdateOnServer.WaitOne();
                        }
                    }

                    eveStatsUpdateOnServer.WaitOne();

                    Assert.AreEqual(0, server.Stats().Stats.OnlineUserCount);

                }
            }
        }

        private void StatsModuleTests_StatsModuleUpdate(object sender, StatsModuleUpdateEventArgs e) {
            throw new NotImplementedException();
        }
    }
}
