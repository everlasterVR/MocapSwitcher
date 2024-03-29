﻿namespace MocapSwitcher
{
    public static class Log
    {
        public static void Error(string message, string name = nameof(Script))
        {
            SuperController.LogError($"{nameof(MocapSwitcher)}.{name}: {message}");
        }

        public static void Message(string message, string name = nameof(Script))
        {
            SuperController.LogMessage($"{nameof(MocapSwitcher)}.{name}: {message}");
        }
    }
}
