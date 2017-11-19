using System;
using System.Diagnostics;
using System.Threading;
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
        public static async Task Run(Func<Task> action, byte retryCount = 3, RetryPolicyBackoff backoff = null, CancellationToken cancellationToken = default(CancellationToken)) {
            action = action ?? throw new ArgumentNullException(nameof(action));
            backoff = backoff ?? DefaultBackoff;

            Exception lastException = null;
            for (var i = 0; i < retryCount; i++) {
                try {
                    if (cancellationToken.IsCancellationRequested) {
                        if(lastException == null)
                            throw new OperationCanceledException(cancellationToken);
                        else throw new OperationCanceledException("Operation cancelled.", lastException, cancellationToken);
                    }

                    await Task.Run(async ()=> await action());
                    return;
                } catch (Exception ex) {
                    if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException("Operation cancelled.", ex, cancellationToken);

                    lastException = ex;

                    if (!(ex is TimeoutException))
                        await Task.Delay(backoff(i));
                }
            }
            throw lastException;
        }
    }
}
