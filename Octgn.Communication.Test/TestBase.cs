using System;
using NUnit.Framework;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Octgn.Communication.Test
{
    [Parallelizable(ParallelScope.None)]
    [NonParallelizable]
    [SetUpFixture]
    public abstract class TestBase
    {
        public static int MaxTimeout => Debugger.IsAttached ? (int)TimeSpan.FromMinutes(30).TotalMilliseconds : (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

        private static TaskCompletionSource<object> _currentTest;
        private static readonly object _locker = new object();

        [SetUp]
        public void Setup() {
            Console.WriteLine($"{nameof(Setup)}: {TestContext.CurrentContext.Test.FullName}");
            lock (_locker) {
                Console.WriteLine($"{nameof(Setup)}: {TestContext.CurrentContext.Test.FullName}: Waiting for current task...");
                _currentTest?.Task.Wait();
                Console.WriteLine($"{nameof(Setup)}: {TestContext.CurrentContext.Test.FullName}: Current task completed.");
                _currentTest = new TaskCompletionSource<object>();
            }

            ConnectionBase.WaitForResponseTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(10);
            LoggerFactory.DefaultMethod = (c) => new InMemoryLogger(c);

            while (InMemoryLogger.LogMessages.Count > 0) {
                InMemoryLogger.LogMessages.TryDequeue(out var result);
            }
        }

        [TearDown]
        public void TearDown() {
            Console.WriteLine($"{nameof(TearDown)}: {TestContext.CurrentContext.Test.FullName}");
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var exceptionCount = Signal.Exceptions.Count;

            if (Signal.Exceptions.Count > 0) {
                Console.WriteLine($"{nameof(TearDown)}: {TestContext.CurrentContext.Test.FullName}: EXCEPTIONS");
                while (Signal.Exceptions.Count > 0) {
                    if (Signal.Exceptions.TryDequeue(out var result)) {
                        Console.WriteLine(result.ToString());
                    }
                }
            }
            if (InMemoryLogger.LogMessages.Count > 0) {
                Console.WriteLine($"{nameof(TearDown)}: {TestContext.CurrentContext.Test.FullName}: LOGS");
                while (InMemoryLogger.LogMessages.Count > 0) {
                    if (InMemoryLogger.LogMessages.TryDequeue(out var result)) {
                        Console.WriteLine(result.ToString());
                    }
                }
            }

            Assert.Zero(exceptionCount, "Unhandled exceptions found in Signal");

            _currentTest.SetResult(null);

            Console.WriteLine($"{nameof(TearDown)}: {TestContext.CurrentContext.Test.FullName}: End of method.");
        }

        [OneTimeTearDown]
        public void OneTimeTeardown() {
            lock (_locker) {
                _currentTest?.Task.Wait();
            }
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, 7920);
        }
    }
}
