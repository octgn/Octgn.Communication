using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public static class ResilliantTask
    {
        private static int DefaultBackoff(int currentRetry) {
            return 1000 * currentRetry;
        }

        public delegate int RetryPolicyBackoff(int currentRetry);

        //[DebuggerStepThrough()]
        public static async Task Run(Func<Task> action, byte retryCount = 3, RetryPolicyBackoff backoff = null) {
            action = action ?? throw new ArgumentNullException(nameof(action));
            backoff = backoff ?? DefaultBackoff;

            Exception lastException = null;
            for (var i = 0; i < retryCount; i++) {
                try {
                    await Task.Run(async ()=> await action());
                    return;
                } catch (Exception ex) {
                    lastException = ex;
                    if(!(ex is TimeoutException))
                        await Task.Delay(backoff(i));
                }
            }
            throw lastException;
        }
    }
}
