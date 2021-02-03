namespace everlaster
{
    public static class Log
    {
        public static void Error(string message, string className = nameof(MocapSwitcher))
        {
            SuperController.LogError($"{nameof(everlaster)}.{className}: {message}");
        }

        public static void Message(string message, string className = nameof(MocapSwitcher))
        {
            SuperController.LogMessage($"{nameof(everlaster)}.{className}: {message}");
        }
    }
}
