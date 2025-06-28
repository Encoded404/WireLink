#if NET9_0
using ConsoleLogger;

namespace WireLink
{
    internal class Logging
    {
        public static void WriteLine(string message)
        {
            Logger.WriteLine(message);
        }
    }
}
#else
using System;

namespace WireLink
{
    internal class Logging
    {
        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
#endif