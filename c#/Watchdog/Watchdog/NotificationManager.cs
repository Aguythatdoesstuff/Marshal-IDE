using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Watchdog
{
    /// <summary>
    /// Manages platform-specific crash notifications.
    /// Supports Windows, Linux, and macOS with appropriate UI frameworks.
    /// </summary>
    public class NotificationManager
    {
        private readonly LogManager _logger;

        // Win32 P/Invoke for MessageBox - works on all Windows systems
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK = 0;
        private const uint MB_ICONERROR = 0x10;

        /// <summary>
        /// Initializes the NotificationManager with a logger instance.
        /// </summary>
        /// <param name="logger">The LogManager instance for logging operations</param>
        public NotificationManager(LogManager logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Displays a crash notification appropriate for the current platform.
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        public void ShowCrashNotification(string title, string message)
        {
            try
            {
                _logger.LogMessage("=== Attempting to show crash notification ===");
                _logger.LogMessage($"Notification Title: {title}");
                _logger.LogMessage($"Notification Message (truncated): {message.Substring(0, Math.Min(100, message.Length))}...");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.LogMessage("Detected Windows platform - attempting PowerShell notification");
                    ShowNotificationWindows(title, message);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    _logger.LogMessage("Detected Linux platform - attempting zenity notification");
                    ShowNotificationLinux(title, message);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _logger.LogMessage("Detected macOS platform - attempting osascript notification");
                    ShowNotificationMacOS(title, message);
                }

                _logger.LogMessage("User notification displayed");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Error showing notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a notification on Windows using Win32 MessageBox API.
        /// This works on ALL Windows systems regardless of language.
        /// Falls back to creating and opening a notification file if MessageBox fails.
        /// </summary>
        private void ShowNotificationWindows(string title, string message)
        {
            try
            {
                _logger.LogMessage("Attempting to show Windows MessageBox notification...");

                // Use Win32 MessageBox directly - works on all Windows systems
                // IntPtr.Zero = no parent window, MB_OK | MB_ICONERROR = OK button + error icon
                int result = MessageBox(IntPtr.Zero, message, title, MB_OK | MB_ICONERROR);

                _logger.LogMessage($"MessageBox returned: {result}");
                _logger.LogMessage("Windows notification displayed successfully");
            }
            catch (Exception ex)
            {
                // Fallback: Create and open a notification file
                _logger.LogMessage($"Windows MessageBox failed: {ex.Message}. Attempting file fallback...");
                ShowNotificationFileFallback(title, message);
            }
        }

        /// <summary>
        /// Shows a notification on Linux using zenity or notify-send.
        /// Falls back to creating and opening a notification file if neither tool is available.
        /// </summary>
        private void ShowNotificationLinux(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "zenity",
                    Arguments = $@"--error --title ""{title}"" --text ""{message}""",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                }
            }
            catch
            {
                // Fallback: Create and open a notification file
                ShowNotificationFileFallback(title, message);
            }
        }

        /// <summary>
        /// Shows a notification on macOS using osascript.
        /// Falls back to creating and opening a notification file if osascript fails.
        /// </summary>
        private void ShowNotificationMacOS(string title, string message)
        {
            try
            {
                string script = $@"tell app ""System Events""
    display alert ""{title}"" message ""{message}"" buttons {{""OK""}} default button ""OK""
end tell";

                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $@"-e '{script}'",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                }
            }
            catch
            {
                // Fallback: Create and open a notification file
                ShowNotificationFileFallback(title, message);
            }
        }

        /// <summary>
        /// Fallback notification method: Creates a temporary text file with the message
        /// and opens it in the user's default text editor.
        /// </summary>
        private void ShowNotificationFileFallback(string title, string message)
        {
            try
            {
                // Create a temp file in the temp directory
                string tempFileName = $"Marshal_IDE_Notification_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                string tempPath = Path.Combine(Path.GetTempPath(), tempFileName);

                // Write the notification content to the file
                string content = $"{title}\n{new string('=', title.Length)}\n\n{message}";
                File.WriteAllText(tempPath, content);

                // Open the file with the default application
                var psi = new ProcessStartInfo(tempPath)
                {
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    // Don't wait for the editor to close
                }

                _logger.LogMessage($"Notification file created and opened: {tempPath}");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Fallback notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Escapes special characters in strings for PowerShell.
        /// </summary>
        private string EscapePowerShellString(string str)
        {
            return str.Replace("'", "''").Replace("\"", "`\"");
        }
    }
}
