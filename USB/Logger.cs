using System;

namespace ledartstudio
{
    internal static class Logger
    {
        internal const int LOG_INFO = 1;
        internal const int LOG_DBG = 2;
        internal const int LOGGING_LEVEL = LOG_INFO;

        internal static void Log(string msg, int logLevel)
        {
            if (logLevel <= LOGGING_LEVEL)
            {
                Console.Write(msg);
            }
        }
    }
}
