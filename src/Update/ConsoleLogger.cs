using Squirrel.SimpleSplat;
using System;

namespace Squirrel.Update
{
    internal class ConsoleLogger : SimpleSplat.ILogger
    {
        public LogLevel Level { get; set; }

        public void Write(string message, LogLevel logLevel)
        {
            if (logLevel >= Level)
            {
                ((logLevel > LogLevel.Warn) ? Console.Error : Console.Out).WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}> {message}");
            }
        }
    }
}
