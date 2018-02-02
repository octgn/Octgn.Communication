using NUnit.Framework;
using Octgn.Communication.Modules;
using Octgn.Communication.Modules.SubscriptionModule;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port)), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new StatsModule(server));

                server.IsEnabled = true;

                using (var clientA = new TestClient(port, new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new TestClient(port, new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeStatsModule();
                    clientB.InitializeStatsModule();

                    using (var eveStatsReceived = new AutoResetEvent(false)) {

                        clientA.Stats().StatsModuleUpdate += (sender, args) => {
                            eveStatsReceived.Set();
                        };

                        await clientA.Connect();

                        eveStatsReceived.WaitOne(1000);

                        Assert.NotNull(clientA.Stats().Stats);
                        Assert.AreEqual(1, clientA.Stats().Stats.OnlineUserCount);

                        await clientB.Connect();

                        eveStatsReceived.WaitOne(1000);

                        Assert.AreEqual(2, clientA.Stats().Stats.OnlineUserCount);

                        clientB.Dispose();

                        eveStatsReceived.WaitOne(1000);

                        Assert.AreEqual(1, clientA.Stats().Stats.OnlineUserCount);
                    }
                }

                Assert.AreEqual(0, server.Stats().Stats.OnlineUserCount);
            }
        }
    }
}
