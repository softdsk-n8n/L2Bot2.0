using System;
using System.IO;
using System.Threading;

namespace Client.Domain.AI
{
    public static class DebugLogger
    {
        private static readonly string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_bot.log");
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public static void Log(string message)
        {
            if (!Enabled)
            {
                return;
            }

            _ = semaphore.WaitAsync().ContinueWith(_ =>
            {
                try
                {
                    var line = $"{DateTime.Now:HH:mm:ss.fff} [{Thread.CurrentThread.ManagedThreadId}] {message}{Environment.NewLine}";
                    File.AppendAllText(logPath, line);
                }
                catch { /* ignore write errors */ }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public static bool Enabled { get; set; } = false;
    }
}
