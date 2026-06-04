using System;
using System.IO;
using System.Reflection;

namespace DynamicCombat
{
    public static class ModLogger
    {
        private static readonly string LogFilePath;
        private static bool _isInitialized = false;
        private static object _lockObj = new object();

        static ModLogger()
        {
            try
            {
                string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                LogFilePath = Path.Combine(assemblyFolder, "DynamicCombat_DebugLog.txt");
            }
            catch
            {
                LogFilePath = "DynamicCombat_DebugLog.txt";
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lockObj)
                {
                    if (!_isInitialized)
                    {
                        File.WriteAllText(LogFilePath, $"--- Dynamic Combat Session Started: {DateTime.Now} ---\n");
                        _isInitialized = true;
                    }
                    
                    string formattedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
                    File.AppendAllText(LogFilePath, formattedMessage);
                }
            }
            catch
            {
                // Ignore logging errors to prevent crashing the game
            }
        }
    }
}
