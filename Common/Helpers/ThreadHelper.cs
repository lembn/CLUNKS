using System;
using System.Threading;

namespace Common.Helpers
{
    /// <summary>
    /// A helper class containing methods for assiting with thread based operations
    /// </summary>
    public static class ThreadHelper
    {
        /// <summary>
        /// Get Endless Cancellable Thread
        /// An Endless Cancellable Thread is a thread that will run forever until it's cancellation
        /// token requests cancellation. GetECThread wraps a method and performs it continually until
        /// cancellation is requested.
        /// </summary>
        /// <param name="ctoken">The CancellationToken</param>
        /// <param name="method">The method to be wrapped. Must take no parameters and return void.</param>
        /// <returns>The ECThread created from the CancellationToken and wrapped method.</returns>
        public static Thread GetECThread(CancellationToken ctoken, Action method)
        {
            var thread = new Thread(() => { 
                while (true && !ctoken.IsCancellationRequested)
                {
                    method();
                    Thread.Sleep(10);
                }
            });
            return thread;
        }
    }
}
