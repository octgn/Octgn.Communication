using System;
using NUnit.Framework;
using System.Net;
using System.Diagnostics;

namespace Octgn.Communication.Test
{
    [Parallelizable(ParallelScope.None)]
    public abstract class TestBase
    {
        public static int MaxTimeout => Debugger.IsAttached ? (int)TimeSpan.FromMinutes(30).TotalMilliseconds : (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

        [SetUp]
        public void Setup() {
            ConnectionBase.WaitForResponseTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(10);
            LoggerFactory.DefaultMethod = (c) => new InMemoryLogger(c);

            while (InMemoryLogger.LogMessages.Count > 0) {
                InMemoryLogger.LogMessages.TryDequeue(out var result);
            }
        }

        [TearDown]
        public void TearDown() {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var exceptionCount = Signal.Exceptions.Count;

            Console.WriteLine("===== EXCEPTIONS ======");
            while(Signal.Exceptions.Count > 0) {
                if(Signal.Exceptions.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }
            Console.WriteLine("===== LOGS ======");
            while (InMemoryLogger.LogMessages.Count > 0) {
                if (InMemoryLogger.LogMessages.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }

            Assert.Zero(exceptionCount, "Unhandled exceptions found in Signal");
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, 7920);
        }
    }
}
