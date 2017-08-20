using System;
using NUnit.Framework;
using System.Net;

namespace Octgn.Communication.Test
{
    [Parallelizable(ParallelScope.None)]
    public abstract class TestBase
    {
        [SetUp]
        public void Setup() {
            ConnectionBase.WaitForResponseTimeout = TimeSpan.FromSeconds(10);
#if (DEBUG)
            while (NullLogger.LogMessages.Count > 0) {
                if (NullLogger.LogMessages.TryDequeue(out var result)) {
                }
            }
#endif
        }

        [TearDown]
        public void TearDown() {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Console.WriteLine("===== EXCEPTIONS ======");
            while(Signal.Exceptions.Count > 0) {
                if(Signal.Exceptions.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }
#if (DEBUG)
            Console.WriteLine("===== LOGS ======");
            while (NullLogger.LogMessages.Count > 0) {
                if (NullLogger.LogMessages.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }
#endif
            Assert.Zero(Signal.Exceptions.Count, "Unhandled exceptions found in Signal");
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, 7920);
        }
    }
}
