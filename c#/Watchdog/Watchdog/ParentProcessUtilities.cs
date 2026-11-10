using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Watchdog
{
    /// <summary>
    /// A utility class to determine a process parent.
    /// Works on both Windows and Linux.
    /// </summary>
    public static class ParentProcessUtilities
    {
        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class, or null if not found.</returns>
        public static Process GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class, or null if not found.</returns>
        public static Process GetParentProcess(int id)
        {
            try
            {
                Process process = Process.GetProcessById(id);
                return GetParentProcess(process.Handle);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class, or null if not found.</returns>
        public static Process GetParentProcess(IntPtr handle)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetParentProcessWindows(handle);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetParentProcessLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetParentProcessLinux(); // macOS uses similar /proc structure
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the parent process on Windows using ntdll.dll.
        /// </summary>
        private static Process GetParentProcessWindows(IntPtr handle)
        {
            try
            {
                var pbi = new ProcessBasicInformation();
                int returnLength;
                int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);

                if (status != 0)
                {
                    throw new ApplicationException($"NtQueryInformationProcess failed with status {status}");
                }

                try
                {
                    return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
                }
                catch (ArgumentException)
                {
                    // Process not found
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the parent process on Linux/Unix systems using /proc filesystem.
        /// </summary>
        private static Process GetParentProcessLinux()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string statFile = $"/proc/{currentPid}/stat";

                if (!File.Exists(statFile))
                {
                    return null;
                }

                string content = File.ReadAllText(statFile).Trim();
                // Parse the /proc/[pid]/stat file
                // Format: pid (comm) state ppid ...
                // The parent PID is the 4th field
                int startParen = content.IndexOf('(');
                int endParen = content.LastIndexOf(')');

                if (startParen < 0 || endParen < 0)
                {
                    return null;
                }

                string afterComm = content.Substring(endParen + 1).Trim();
                string[] parts = afterComm.Split(' ');

                if (parts.Length > 1)
                {
                    if (int.TryParse(parts[1], out int parentPid) && parentPid > 0)
                    {
                        try
                        {
                            return Process.GetProcessById(parentPid);
                        }
                        catch (ArgumentException)
                        {
                            // Process not found
                            return null;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Represents the structure used by NtQueryInformationProcess on Windows.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            internal IntPtr Reserved1;
            internal IntPtr PebBaseAddress;
            internal IntPtr Reserved2_0;
            internal IntPtr Reserved2_1;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;
        }

        /// <summary>
        /// P/Invoke declaration for NtQueryInformationProcess.
        /// </summary>
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            int processInformationLength,
            out int returnLength);
    }
}
