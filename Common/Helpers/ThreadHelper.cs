using System;
using System.Threading;

namespace Common.Helpers
{
    public static class ThreadHelper
    {
        public static Thread GetECThread(CancellationToken ctoken, Action method)
        {
            var thread = new Thread(() => { 
                while (true && !ctoken.IsCancellationRequested)
                {
                    method();
                }
            });
            return thread;
        }
    }
}
