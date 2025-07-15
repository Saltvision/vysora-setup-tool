using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace Vysora
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        None
    }

    public static class Logger
    {
        private static LogLevel _logLevel = LogLevel.Info;
        private static bool _logToFile = false;
        private static string _logFilePath = "Logs/vysora_plugin.log";
        private static StringBuilder _logBuffer = new StringBuilder();

        public static LogLevel LogLevel
        {
            get { return _logLevel; }
            set { _logLevel = value; }
        }

        public static bool LogToFile
        {
            get { return _logToFile; }
            set
            {
                _logToFile = value;
                if (_logToFile)
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(_logFilePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
            }
        }

        public static string LogFilePath
        {
            get { return _logFilePath; }
            set { _logFilePath = value; }
        }

        public static void Debug(string message, bool consoleOnly = false)
        {
            if (_logLevel <= LogLevel.Debug)
            {
                UnityEngine.Debug.Log($"[Vysora:Debug] {message}");
                LogToFileInternal("DEBUG", message, consoleOnly);
            }
        }

        public static void Info(string message, bool consoleOnly = false)
        {
            if (_logLevel <= LogLevel.Info)
            {
                UnityEngine.Debug.Log($"[Vysora:Info] {message}");
                LogToFileInternal("INFO", message, consoleOnly);
            }
        }

        public static void Warning(string message, bool consoleOnly = false)
        {
            if (_logLevel <= LogLevel.Warning)
            {
                UnityEngine.Debug.LogWarning($"[Vysora:Warning] {message}");
                LogToFileInternal("WARNING", message, consoleOnly);
            }
        }

        public static void Error(string message, bool consoleOnly = false)
        {
            if (_logLevel <= LogLevel.Error)
            {
                UnityEngine.Debug.LogError($"[Vysora:Error] {message}");
                LogToFileInternal("ERROR", message, consoleOnly);
            }
        }

        public static void Exception(Exception ex, string context = null, bool consoleOnly = false)
        {
            if (_logLevel <= LogLevel.Error)
            {
                string message = string.IsNullOrEmpty(context) ?
                    $"Exception: {ex.Message}" :
                    $"Exception in {context}: {ex.Message}";

                UnityEngine.Debug.LogError($"[Vysora:Exception] {message}\n{ex.StackTrace}");
                LogToFileInternal("EXCEPTION", $"{message}\n{ex.StackTrace}", consoleOnly);
            }
        }

        private static void LogToFileInternal(string level, string message, bool consoleOnly)
        {
            if (_logToFile && !consoleOnly)
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                _logBuffer.AppendLine(logMessage);

                // Flush to file if buffer gets too large
                if (_logBuffer.Length > 4096)
                {
                    FlushLogBuffer();
                }
            }
        }

        public static void FlushLogBuffer()
        {
            if (_logToFile && _logBuffer.Length > 0)
            {
                try
                {
                    File.AppendAllText(_logFilePath, _logBuffer.ToString());
                    _logBuffer.Clear();
                }
                catch (Exception ex)
                {
                    // Just print to console if we can't write to file
                    UnityEngine.Debug.LogError($"Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}