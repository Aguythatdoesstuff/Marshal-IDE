using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Watchdog
{
    internal class Program
    {
        /// <summary>
        /// Easy toggle for test mode - set to true to enable simulated stderr testing.
        /// WARNING: Must be set to false before publishing/release builds.
        /// The compiler will throw an error if this is true in Release mode.
        /// </summary>
        private const bool ENABLE_TEST_MODE = false;

        private static LogManager _logger;
        private static CancellationTokenSource _cts;

        static void Main(string[] args)
        {
            // Compile-time safety check: prevent accidental release builds with test mode enabled
#if !DEBUG
            if (ENABLE_TEST_MODE)
            {
                throw new InvalidOperationException("ERROR: TEST MODE IS ENABLED! Set ENABLE_TEST_MODE to false before publishing. Test mode must only run in Debug builds.");
            }
#endif

            try
            {
                // Parse command-line arguments
                var argumentParser = new ArgumentParser();
                string metadataPath = argumentParser.ParseArguments(args);

                // Setup paths
                var pathManager = new PathManager();
                if (!pathManager.Initialize(metadataPath))
                {
                    ShowErrorAndExit(
                        $"Fatal Error: Cannot access or create metadata directory at:\n{metadataPath}\n\n" +
                        "Usage: Watchdog.exe metadataPath=<absolute_path>\n" +
                        "Or run in debug mode (with debugger attached) to use current process directory.",
                        1);
                    return;
                }

                // Setup logging
                _logger = new LogManager(pathManager.WatchdogLogPath);
                if (!_logger.Open())
                {
                    ShowErrorAndExit(
                        $"Fatal Error: Cannot open log file at:\n{pathManager.WatchdogLogPath}",
                        1);
                    return;
                }

                // Log startup information
                _logger.LogMessage("=== Watchdog Process Started ===");
                _logger.LogMessage($"Metadata Path: {pathManager.MetadataPath}");
                _logger.LogMessage($"Log File: {pathManager.WatchdogLogPath}");
                _logger.LogMessage($"Debug Mode: {pathManager.IsDebugMode}");
                _logger.LogMessage($"Process ID: {Process.GetCurrentProcess().Id}");
                _logger.LogMessage("");

                // Initialize components
                var processManager = new ProcessManager(_logger);
                var notificationManager = new NotificationManager(_logger);

                // Determine test mode: use ENABLE_TEST_MODE constant OR check command-line args
                bool testMode = ENABLE_TEST_MODE || 
                    (args != null && System.Array.Exists(args, arg => arg.Equals("--test-mode", StringComparison.OrdinalIgnoreCase)));

                _logger.LogMessage($"Test Mode: {testMode}");
                if (testMode)
                {
                    _logger.LogMessage("Running in TEST MODE - simulating parent process stderr");
                }
                _logger.LogMessage("");

                var stderrMonitor = new StderrMonitor(_logger, processManager, notificationManager, pathManager.WatchdogLogPath, testMode);

                // Start monitoring
                _cts = new CancellationTokenSource();
                stderrMonitor.MonitorStderr(_cts.Token).Wait();
            }
            catch (Exception ex)
            {
                _logger?.LogMessage($"Watchdog Exception: {ex}");
                Environment.Exit(1);
            }
            finally
            {
                _logger?.Dispose();
            }
        }

        private static void ShowErrorAndExit(string message, int exitCode)
        {
            Console.Error.WriteLine(message);
            Environment.Exit(exitCode);
        }
    }
}
