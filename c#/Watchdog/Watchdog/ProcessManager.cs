using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Watchdog
{
    /// <summary>
    /// Manages process termination with proper signal escalation.
    /// SIGTERM -> SIGINT -> SIGKILL (last resort)
    /// </summary>
    public class ProcessManager
    {
        private readonly LogManager _logger;

        /// <summary>
        /// Initializes the ProcessManager with a logger instance.
        /// </summary>
        /// <param name="logger">The LogManager instance for logging operations</param>
        public ProcessManager(LogManager logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts graceful shutdown of a process using SIGTERM signal.
        /// SIGTERM -> SIGINT -> SIGKILL escalation.
        /// </summary>
        /// <param name="process">The process to shut down</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task AttemptGracefulShutdown(Process process)
        {
            if (process == null)
            {
                _logger.LogMessage("Error: Cannot shutdown null process");
                return;
            }

            // Step 1: SIGTERM (graceful shutdown)
            _logger.LogMessage($"Sending SIGTERM to process {process.Id}...");
            if (await AttemptShutdownWithSignal(process, "SIGTERM"))
            {
                return;  // Process shut down gracefully
            }

            // Step 2: SIGINT (Ctrl+C)
            _logger.LogMessage($"SIGTERM failed. Sending SIGINT to process {process.Id}...");
            if (await AttemptShutdownWithSignal(process, "SIGINT"))
            {
                return;  // Process shut down with SIGINT
            }

            // Step 3: SIGKILL (forceful kill - only if not in debug mode)
            _logger.LogMessage($"SIGINT failed. Attempting SIGKILL on process {process.Id}...");
            AttemptForcefulKill(process);
        }

        /// <summary>
        /// Sends a specific signal to the process platform-appropriately.
        /// </summary>
        /// <param name="process">The process to signal</param>
        /// <param name="signal">Signal name: SIGTERM, SIGINT, or SIGKILL</param>
        /// <returns>True if process exited, false if it's still running</returns>
        private async Task<bool> AttemptShutdownWithSignal(Process process, string signal)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await AttemptWindowsSignal(process, signal);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await AttemptUnixSignal(process, signal);
                }

                // Wait to see if process exits
                await Task.Run(() => process.WaitForExit(2000));

                if (process.HasExited)
                {
                    _logger.LogMessage($"{signal} succeeded - process {process.Id} exited");
                    return true;
                }

                _logger.LogMessage($"{signal} sent but process {process.Id} still running");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Error sending {signal}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends signal to process on Windows using modern .NET approach.
        /// </summary>
        private async Task AttemptWindowsSignal(Process process, string signal)
        {
            try
            {
                if (signal == "SIGTERM")
                {
                    // SIGTERM on Windows = graceful window close
                    _logger.LogMessage($"Windows: Sending WM_CLOSE to process {process.Id}");
                    process.CloseMainWindow();
                }
                else if (signal == "SIGINT")
                {
                    // SIGINT on Windows = Ctrl+C via taskkill
                    _logger.LogMessage($"Windows: Sending Ctrl+C equivalent via taskkill to process {process.Id}");
                    var psi = new ProcessStartInfo("taskkill.exe", $"/PID {process.Id}")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        await Task.Run(() => proc?.WaitForExit(2000));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Windows {signal} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends signal to process on Unix/Linux using kill command.
        /// </summary>
        private async Task AttemptUnixSignal(Process process, string signal)
        {
            try
            {
                string killSignal = signal switch
                {
                    "SIGTERM" => "-TERM",
                    "SIGINT" => "-INT",
                    "SIGKILL" => "-KILL",
                    _ => "-TERM"
                };

                _logger.LogMessage($"Unix: Sending {signal} ({killSignal}) to process {process.Id}");

                var psi = new ProcessStartInfo("kill", $"{killSignal} {process.Id}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    await Task.Run(() => proc?.WaitForExit(2000));
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Unix {signal} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts forceful termination of a process (SIGKILL - last resort).
        /// In DEBUG mode, only logs without actually killing to protect IDE.
        /// ONLY kills the specific process, NOT the entire tree (to avoid killing watchdog itself).
        /// </summary>
        /// <param name="process">The process to kill</param>
        public void AttemptForcefulKill(Process process)
        {
            if (process == null)
            {
                _logger.LogMessage("Error: Cannot kill null process");
                return;
            }

            try
            {
#if DEBUG
                _logger.LogMessage($"[DEBUG MODE] Skipping forceful kill of process {process.Id} to prevent IDE termination");
                return;
#endif

                _logger.LogMessage($"Sending SIGKILL to process {process.Id} (last resort)...");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Modern .NET way: Kill ONLY this process (not tree, to avoid killing watchdog)
                    _logger.LogMessage($"Windows: Killing process {process.Id} (single process only)");
                    process.Kill(entireProcessTree: false);
                    _logger.LogMessage($"Process {process.Id} killed");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Unix: Use kill -KILL (SIGKILL) - only kills the single process
                    _logger.LogMessage($"Unix: Killing process {process.Id} with SIGKILL (single process only)");
                    var psi = new ProcessStartInfo("kill", $"-KILL {process.Id}")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(2000);
                    }

                    _logger.LogMessage($"Process {process.Id} killed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Error during forceful kill: {ex.Message}");
                _logger.LogMessage($"Process {process.Id} may still be alive - giving up");
            }
        }
    }
}
