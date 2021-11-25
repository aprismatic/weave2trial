using System;
using System.Diagnostics.CodeAnalysis;

namespace weave2trial
{
    public static class Log
    {
        public static void Info(string msg) {
            var ts = DateTime.Now.ToString("HH:mm:ss.fffff");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("INFO ");
            Output(msg);
        }

        public static void Error(string msg) {
            var ts = DateTime.Now.ToString("HH:mm:ss.fffff");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("ERROR");
            Output(msg);
        }

        [DoesNotReturn]
        public static void ErrorAndThrow(string msg) {
            Error(msg);
            throw new ApplicationException(msg);
        }

        private static void Output(string msg) {
            var ts = DateTime.Now.ToString("HH:mm:ss.fffff");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($" [{ts}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(msg);
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
