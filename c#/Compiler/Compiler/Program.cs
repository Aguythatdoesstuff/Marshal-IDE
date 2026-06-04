using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Compiler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Register global handlers to ensure we always have an Exception object and report via IPC
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception ?? new Exception("Unhandled exception without Exception object");
                    IPC.Send("FatalError", ex.Message);
                    Compiler.Logging.Logger.ReportUnhandledException(ex);
                    // Exit non-zero to indicate fatal crash
                    Environment.Exit(1);
                }
                catch { Environment.Exit(1); }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    var ex = e.Exception ?? new Exception("UnobservedTaskException");
                    IPC.Send("FatalError", ex.Message);
                    Compiler.Logging.Logger.ReportUnhandledException(ex);
                    e.SetObserved();
                }
                catch { }
            };

            // Initialize logger early so other components can use it
            string debugArg = null;
            if (args != null && args.Length > 0)
            {
                debugArg = args.FirstOrDefault(a => a.StartsWith("--debuglog=", StringComparison.OrdinalIgnoreCase));
            }

            string resolvedLogPath = null;
            if (!string.IsNullOrWhiteSpace(debugArg))
            {
                var raw = debugArg.Substring("--debuglog=".Length).Trim();
                try
                {
                    if (Path.IsPathRooted(raw)) resolvedLogPath = Path.GetFullPath(raw);
                }
                catch { resolvedLogPath = null; }
            }

            if (string.IsNullOrWhiteSpace(resolvedLogPath))
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                resolvedLogPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "logs");
            }

            try
            {
                Compiler.Logging.Logger.Initialize(resolvedLogPath);
            }
            catch
            {
                // If logger initialization fails for any reason, continue without crashing the whole app
            }

            IEnumerable<string> filePaths = Array.Empty<string>();

            string outputArg = null;
            if (args != null && args.Length > 0)
            {
                outputArg = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
            }

            string resolvedOutputPath;
            if (!string.IsNullOrWhiteSpace(outputArg))
            {
                var raw = outputArg.Substring("--output=".Length).Trim();
                if (!Path.IsPathRooted(raw))
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] --output must be an absolute path. Please provide an absolute path.");
                    return;
                }

                try
                {
                    resolvedOutputPath = Path.GetFullPath(raw);
                }
                catch
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] Failed to normalize provided --output path. Provide a valid absolute path.");
                    return;
                }
            }
            else
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                resolvedOutputPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "compiled-data");
            }

            BaseCompiler.OutputPath = resolvedOutputPath;
            try
            {
                Directory.CreateDirectory(BaseCompiler.OutputPath);
            }
            catch { }


            // Remaining args (excluding the --output= arg) are treated as file paths.
            if (args != null && args.Length > 0)
            {
                var remaining = args.Where(a => !string.IsNullOrWhiteSpace(outputArg) ? !a.Equals(outputArg, StringComparison.OrdinalIgnoreCase) : true).ToArray();
                if (remaining.Length > 0)
                {
                    filePaths = remaining.Select(Path.GetFullPath).ToArray();
                }
                else
                {
                    // No file paths supplied, fallback to discovery of test input files
                    filePaths = DiscoverTestFiles();
                }
            }
            else
            {
                // No args at all: fallback to discovery of test input files
                filePaths = DiscoverTestFiles();
            }

            if (!filePaths.Any())
            {
                Compiler.Logging.Logger.LogMain("No valid files to process. Exiting.");
                return;
            }

            var manager = new ProcessManager();
            manager.ProcessFiles(filePaths);
        }

        private static IEnumerable<string> DiscoverTestFiles()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var testDir = Path.Combine(desktop, "marshal-ide-compiler-debbug", "input");

            if (!Directory.Exists(testDir))
            {
                Compiler.Logging.Logger.LogMain("Test directory not found: " + testDir);
                try
                {
                    Directory.CreateDirectory(testDir);
                    Compiler.Logging.Logger.LogMain("Created test directory: " + testDir);
                }
                catch
                {
                    // ignore create failure, fall through to return
                }
                return Array.Empty<string>();
            }

            Compiler.Logging.Logger.LogMain("Scanning test directory: " + testDir);

            var allowedExtensions = new[] { ".decision", ".event", ".focus", ".idea", ".scriptedgui", ".script" };
            var files = new List<string>();

            foreach (var ext in allowedExtensions)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + ext, SearchOption.AllDirectories));
            }

            Compiler.Logging.Logger.LogMain($"Found {files.Count} valid file(s) in {testDir}\n");
            return files.Select(Path.GetFullPath).ToArray();
        }


    }
}