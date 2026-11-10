using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Watchdog
{
    /// <summary>
    /// Monitors the parent process's stderr stream and handles fatal errors.
    /// Supports both real stdin piping and test mode simulation.
    /// </summary>
    public class StderrMonitor
    {
        private readonly LogManager _logger;
        private readonly ProcessManager _processManager;
        private readonly NotificationManager _notificationManager;
        private readonly string _crashReportPath;
        private readonly bool _testMode;
        private DateTime _lastStderrTime;

        /// <summary>
        /// Initializes the StderrMonitor.
        /// </summary>
        /// <param name="logger">The LogManager instance</param>
        /// <param name="processManager">The ProcessManager instance</param>
        /// <param name="notificationManager">The NotificationManager instance</param>
        /// <param name="watchdogLogPath">The path to the watchdog log file</param>
        /// <param name="testMode">If true, simulates parent stderr instead of reading from stdin</param>
        public StderrMonitor(
            LogManager logger,
            ProcessManager processManager,
            NotificationManager notificationManager,
            string watchdogLogPath,
            bool testMode = false)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _notificationManager = notificationManager ?? throw new ArgumentNullException(nameof(notificationManager));
            _crashReportPath = watchdogLogPath ?? throw new ArgumentNullException(nameof(watchdogLogPath));
            _testMode = testMode;
            _lastStderrTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Monitors stderr and handles fatal errors.
        /// In test mode, simulates stderr input from a parent process.
        /// In production, reads actual piped stdin from parent process.
        /// </summary>
        /// <param name="cancellationToken">Token for cancellation</param>
        /// <returns>A task representing the monitoring operation</returns>
        public async Task MonitorStderr(CancellationToken cancellationToken)
        {
            if (_testMode)
            {
                await MonitorStderrTestMode(cancellationToken);
            }
            else
            {
                await MonitorStderrProduction(cancellationToken);
            }
        }
        /// <summary>
        /// Production mode: reads actual piped stderr from parent process via stdin.
        /// </summary>
        private async Task MonitorStderrProduction(CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Console.OpenStandardInput()))
            {
                string line;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        _logger.LogMessage($"[STDERR] {line}");
                        _lastStderrTime = DateTime.UtcNow;
                    }

                    // Check if timeout has passed (no stderr for 1 second)
                    if ((DateTime.UtcNow - _lastStderrTime).TotalMilliseconds > 1000)
                    {
                        // Fatal error detected - escalate termination
                        await HandleFatalError();
                        return;
                    }

                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }

                // End of stdin reached (parent process terminated unexpectedly)
                await HandleFatalError();
            }
        }

        /// <summary>
        /// Test mode: simulates parent process stderr by injecting mock lines.
        /// This tests the full pipeline without needing an actual parent process.
        /// </summary>
        private async Task MonitorStderrTestMode(CancellationToken cancellationToken)
        {
            _logger.LogMessage("=== TEST MODE: Simulating parent process stderr ===");

            // Simulate lines coming from parent process
            string[] simulatedStderrLines = new[]
            {
                "[PARENT] Application started successfully",
                "[PARENT] Loading configuration...",
                "[PARENT] ERROR: Database connection failed",
                "[PARENT] ERROR: Attempting reconnect...",
                "[PARENT] FATAL: Max retries exceeded, crashing",
            };

            _logger.LogMessage($"Injecting {simulatedStderrLines.Length} simulated stderr lines...");

            foreach (var line in simulatedStderrLines)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Simulate a line being received
                _logger.LogMessage($"[STDERR] {line}");
                _lastStderrTime = DateTime.UtcNow;

                // Simulate realistic timing between lines
                await Task.Delay(100, cancellationToken);
            }

            // Now simulate the parent process ending (no more stderr coming in)
            _logger.LogMessage("Simulated parent process stderr ended - waiting for timeout...");

            // Wait to trigger the 1-second timeout
            await Task.Delay(1500, cancellationToken);

            // Timeout triggered - handle fatal error
            _logger.LogMessage("Timeout reached (1 second with no new stderr) - triggering fatal error handling");
            await HandleFatalError();
        }

        /// <summary>
        /// Handles fatal error by terminating parent process and generating crash report.
        /// </summary>
        private async Task HandleFatalError()
        {
            _logger.LogMessage("");
            _logger.LogMessage("=== FATAL ERROR DETECTED ===");
            _logger.LogMessage($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logger.LogMessage("");

            // Get parent process
            var parentProcess = ParentProcessUtilities.GetParentProcess();

            if (parentProcess != null)
            {
                try
                {
                    _logger.LogMessage($"Parent Process ID: {parentProcess.Id}");
                    _logger.LogMessage($"Parent Process Name: {parentProcess.ProcessName}");
                    _logger.LogMessage("Attempting graceful shutdown (SIGTERM -> SIGINT -> SIGKILL)...");

                    // AttemptGracefulShutdown handles the full escalation:
                    // SIGTERM -> SIGINT -> SIGKILL
                    await _processManager.AttemptGracefulShutdown(parentProcess);
                }
                catch (Exception ex)
                {
                    _logger.LogMessage($"Error handling parent process: {ex.Message}");
                }
                finally
                {
                    parentProcess?.Dispose();
                }
            }
            else
            {
                _logger.LogMessage("Warning: Could not obtain parent process");
            }

            _logger.LogMessage("");
            _logger.LogMessage("=== Generating Crash Report ===");

            // Copy log to desktop
            CopyCrashReportToDesktop();

            // Show notification
            _notificationManager.ShowCrashNotification(
                "Marshal IDE - Fatal Error",
                @"Marshal IDE has encountered a fatal error and has been terminated by the watchdog.

A crash report has been saved to your Desktop as:
'Marshal IDE crash report.txt'

Please help us fix this by reporting the crash:
1. Check https://github.com/Aguythatdoesstuff/Marshal-IDE/issues
2. Or report on our Discord server

Thank you for using Marshal IDE!");

            _logger.LogMessage("Watchdog exit code: 0");
            Environment.Exit(0);
        }

        /// <summary>
        /// Copies the crash report (watchdog.log) to the user's desktop.
        /// </summary>
        private void CopyCrashReportToDesktop()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string crashReportDestination = Path.Combine(desktopPath, "Marshal IDE crash report.txt");

                _logger.Flush();
                File.Copy(_crashReportPath, crashReportDestination, true);

                _logger.LogMessage($"Crash report copied to: {crashReportDestination}");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Error copying crash report to desktop: {ex.Message}");
            }
        }
    }
}
