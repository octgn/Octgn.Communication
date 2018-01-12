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
            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: CONFIGURING TEST {TestContext.CurrentContext.Test.FullName}");
            lock (_locker) {
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: WAITING FOR CURRENT TEST");
                _currentTest?.Task.Wait();
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: DONE WAITING FOR CURRENT TEST");
                _currentTest = new TaskCompletionSource<object>();
            }

            ConnectionBase.WaitForResponseTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(10);
            LoggerFactory.DefaultMethod = (c) => new InMemoryLogger(c);

            while (InMemoryLogger.LogMessages.Count > 0) {
                InMemoryLogger.LogMessages.TryDequeue(out var result);
            }
            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Starting test...");
        }

        [TearDown]
        public void TearDown() {
            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Test complete.");
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Cleaned up GC");

            var exceptionCount = Signal.Exceptions.Count;

            if (Signal.Exceptions.Count > 0) {
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: EXCEPTIONS =============================");
                while (Signal.Exceptions.Count > 0) {
                    if (Signal.Exceptions.TryDequeue(out var result)) {
                        Console.WriteLine(result.ToString());
                    }
                }
            }
            if (InMemoryLogger.LogMessages.Count > 0) {
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: LOGS =============================");
                while (InMemoryLogger.LogMessages.Count > 0) {
                    if (InMemoryLogger.LogMessages.TryDequeue(out var result)) {
                        Console.WriteLine(result.ToString());
                    }
                }
            }

            Assert.Zero(exceptionCount, "Unhandled exceptions found in Signal");

            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Signaling test complete.");

            _currentTest.SetResult(null);

            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Very very end of test. Good bye.");
        }

        [OneTimeTearDown]
        public void OneTimeTeardown() {
            lock (_locker) {
                _currentTest?.Task.Wait();
            }
        }

        private static int _currentPort = 7920;
        public static int NextPort => Interlocked.Increment(ref _currentPort);
    }
}
