using System;
using System.Diagnostics;
using System.IO;

namespace Watchdog
{
    /// <summary>
    /// Manages metadata and log file paths.
    /// Supports debug mode where metadata path can be omitted and watchdog.log is placed in the process directory.
    /// </summary>
    public class PathManager
    {
        private string _metadataPath;
        private string _watchdogLogPath;

        /// <summary>
        /// Initializes the PathManager with a metadata path.
        /// In debug mode (Debugger.IsAttached), metadata path can be null and will use process directory.
        /// </summary>
        /// <param name="metadataPath">The metadata directory path</param>
        /// <returns>True if paths were initialized successfully, false otherwise</returns>
        public bool Initialize(string metadataPath)
        {
            try
            {
                // In debug mode, allow missing metadata path
                if (Debugger.IsAttached)
                {
                    if (string.IsNullOrEmpty(metadataPath))
                    {
                        // Use the directory where the current process is located
                        _metadataPath = AppDomain.CurrentDomain.BaseDirectory;
                    }
                    else
                    {
                        _metadataPath = metadataPath;
                    }
                }
                else
                {
                    // In production mode, metadata path is required
                    if (string.IsNullOrEmpty(metadataPath))
                    {
                        return false;
                    }
                    _metadataPath = metadataPath;
                }

                // Ensure metadata directory exists
                if (!Directory.Exists(_metadataPath))
                {
                    Directory.CreateDirectory(_metadataPath);
                }

                // Set the watchdog log path
                _watchdogLogPath = Path.Combine(_metadataPath, "watchdog.log");

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the metadata directory path.
        /// </summary>
        public string MetadataPath => _metadataPath;

        /// <summary>
        /// Gets the watchdog log file path.
        /// </summary>
        public string WatchdogLogPath => _watchdogLogPath;

        /// <summary>
        /// Checks if running in debug mode.
        /// </summary>
        public bool IsDebugMode => Debugger.IsAttached;
    }
}
