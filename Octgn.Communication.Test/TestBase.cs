﻿using System;
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

        private readonly Queue<TaskCompletionSource<object>> _tests = new Queue<TaskCompletionSource<object>>();

        [SetUp]
        public void Setup() {
            _tests.Enqueue(new TaskCompletionSource<object>());
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

            var tcs = _tests.Dequeue();
            tcs.SetResult(null);
        }

        [OneTimeTearDown]
        public void OneTimeTeardown() {
            Task.WhenAll(_tests.Select(t=>t.Task));
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, 7920);
        }
    }
}
