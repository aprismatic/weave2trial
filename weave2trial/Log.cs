using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace weave2trial
{
    public static class Log
    {
        private enum Severity
        {
            INFO,
            WARNING,
            ERROR
        };

        private struct LogEntry
        {
            public LogEntry(Severity severity, string message)
            {
                Severity = severity;
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fffff");
                Message = message;
            }

            public Severity Severity;
            public string Timestamp;
            public string Message;
        }

        private static BlockingCollection<LogEntry> _log;
        private static CancellationTokenSource _cts;
        private static Task _loggerTask;

        static Log() {
            Console.OutputEncoding = Encoding.UTF8;

            AppDomain.CurrentDomain.ProcessExit += Log_Dtor!;

            _log = new(new ConcurrentQueue<LogEntry>());

            _cts = new();
            var ct = _cts.Token;

            _loggerTask = Task.Run(() => mainLoop(ct));
        }

        static void Log_Dtor(object sender, EventArgs e) {
            _cts.Cancel();
            _loggerTask.Wait();
        }

        public static void Info(string msg) {
            _log.Add(new LogEntry(Severity.INFO, msg));
        }

        public static void Error(string msg) {
            _log.Add(new LogEntry(Severity.ERROR, msg));
        }

        [DoesNotReturn]
        public static void ErrorAndThrow(string msg) {
            Error(msg);
            throw new ApplicationException(msg);
        }

        private static void mainLoop(CancellationToken ct) {
            while (true) {
                var res = _log.TryTake(out var entry, 50);

                if (!res) {
                    if (ct.IsCancellationRequested) {
                        return;
                    }
                    continue;
                }

                output(entry);
            }
        }

        private static void output(LogEntry entry) {
            var pushColor = Console.ForegroundColor;

            switch (entry.Severity) {
                case Severity.INFO:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write("INFO ");
                    break;
                case Severity.WARNING:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("WARN ");
                    break;
                case Severity.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("ERROR");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected severity level: " + entry.Severity);
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($" [{entry.Timestamp}] ");

            Console.ForegroundColor = pushColor;
            Console.Write(entry.Message);

            Console.WriteLine();
        }
    }
}
