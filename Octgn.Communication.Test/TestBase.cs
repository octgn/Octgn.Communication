using System;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Serializers;
using System.Collections.Generic;
using Octgn.Communication.Tcp;

namespace Octgn.Communication.Test
{
    [Parallelizable(ParallelScope.None)]
    [NonParallelizable]
    [SetUpFixture]
    public abstract class TestBase
    {
        //public static int MaxTimeout => Debugger.IsAttached ? (int)TimeSpan.FromMinutes(30).TotalMilliseconds : (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
        public static int MaxTimeout => (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

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

            LoggerFactory.DefaultMethod = (c) => new InMemoryLogger(c);
            //ConnectionBase.WaitForResponseTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(10);
            ConnectionBase.WaitForResponseTimeout = TimeSpan.FromSeconds(10);

            Signal.OnException += Signal_OnException;
            Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Starting test...");
        }

        protected List<ExceptionEventArgs> SignalErrors = new List<ExceptionEventArgs>();

        private void Signal_OnException(object sender, ExceptionEventArgs args) {
            SignalErrors.Add(args);
        }

        [TearDown]
        public void TearDown() {
            try {
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Test complete.");

                Signal.OnException -= Signal_OnException;

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Cleaned up GC");

                var exceptionCount = SignalErrors.Count;

                if (exceptionCount > 0) {
                    Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: EXCEPTIONS =============================");
                    foreach (var error in SignalErrors) {
                        Console.WriteLine(error.ToString());
                    }
                }
                if (InMemoryLogger.LogMessages.Count > 0) {
                    Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: LOGS =============================");
                    while (InMemoryLogger.LogMessages.Count > 0) {
                        if (InMemoryLogger.LogMessages.TryDequeue(out var result)) {
                            Console.WriteLine(result);
                        }
                    }
                }

                SignalErrors.Clear();

                Assert.Zero(exceptionCount, "Unhandled exceptions found in Signal");
            } finally {
                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Signaling test complete.");

                _currentTest.SetResult(null);

                Console.WriteLine($"=== {TestContext.CurrentContext.Test.Name}: Very very end of test. Good bye.");
            }
        }

        [OneTimeTearDown]
        public void OneTimeTeardown() {
            lock (_locker) {
                _currentTest?.Task.Wait();
            }
        }

        private static int _currentPort = 7920;
        public static int NextPort => Interlocked.Increment(ref _currentPort);

        protected Client CreateClient(string userId) {
            return new Client(CreateConnectionCreator(userId), new XmlSerializer());
        }

        protected IConnectionCreator CreateConnectionCreator(string userId) {
            return new TcpConnectionCreator(new TestHandshaker(userId));
        }
    }
}
