using System;
using System.IO;

namespace Watchdog
{
    /// <summary>
    /// Manages logging to file and console.
    /// Provides centralized logging with timestamp formatting.
    /// </summary>
    public class LogManager : IDisposable
    {
        private StreamWriter _logWriter;
        private readonly string _logPath;

        /// <summary>
        /// Initializes the LogManager with a log file path.
        /// </summary>
        /// <param name="logPath">The full path to the log file</param>
        public LogManager(string logPath)
        {
            _logPath = logPath;
        }

        /// <summary>
        /// Opens the log file for writing (overwrites existing content).
        /// </summary>
        /// <returns>True if log file was opened successfully, false otherwise</returns>
        public bool Open()
        {
            try
            {
                _logWriter = new StreamWriter(_logPath, false) { AutoFlush = true };
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Logs a message with timestamp.
        /// Empty messages are written as blank lines without timestamps.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogMessage(string message)
        {
            try
            {
                if (_logWriter != null)
                {
                    // Empty messages should just write a blank line, not waste space with timestamp
                    if (string.IsNullOrEmpty(message))
                    {
                        _logWriter.WriteLine();
                    }
                    else
                    {
                        string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                        _logWriter.WriteLine(timestampedMessage);
                    }
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        /// <summary>
        /// Flushes the log buffer to disk.
        /// </summary>
        public void Flush()
        {
            try
            {
                _logWriter?.Flush();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Gets the log file path.
        /// </summary>
        public string LogPath => _logPath;

        /// <summary>
        /// Disposes the log writer.
        /// </summary>
        public void Dispose()
        {
            Flush();
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }
}
