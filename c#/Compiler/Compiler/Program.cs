using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

            // Setup SIGINT (Ctrl+C) handler to exit cleanly if requested
            Console.CancelKeyPress += (sender, e) =>
            {
                Compiler.Logging.Logger.LogMain("SIGINT received. Shutting down compiler process...");
                e.Cancel = false; // Allow the process to terminate smoothly
            };

            // Initialize logger early so other components can use it
            string debugArg = null;
            if (args != null && args.Length > 0)
            {
                debugArg = args.FirstOrDefault(a => a.StartsWith("--debug=", StringComparison.OrdinalIgnoreCase));
            }

            string resolvedLogPath = null;
            if (!string.IsNullOrWhiteSpace(debugArg))
            {
                var raw = debugArg.Substring("--debug=".Length).Trim();
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
            catch { /* Continue without crashing */ }

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
                    Compiler.Logging.Logger.LogMain("[ERROR] --output must be an absolute path.");
                    return;
                }

                try
                {
                    resolvedOutputPath = Path.GetFullPath(raw);
                }
                catch
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] Failed to normalize provided --output path.");
                    return;
                }
            }
            else
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                resolvedOutputPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "compiled-data");
            }

            BaseCompiler.OutputPath = resolvedOutputPath;
            try { Directory.CreateDirectory(BaseCompiler.OutputPath); } catch { }

            var manager = new ProcessManager();
            IPC.Log("Info", $"Compiler initialized and ready to compile. Received Output path:{resolvedOutputPath}     Logs path:{resolvedLogPath}");
            // Enter continuous loop to receive absolute file paths from parent process via stdin
            while (true)
            {
                string input;
                try
                {
                    input = Console.ReadLine();
                }
                catch (IOException ex)
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] Failed to read input: " + ex.Message);
                    System.Threading.Thread.Sleep(50);
                    continue;
                }

                // FIX: If input is null, standard input stream has closed (Parent process terminated or pipe broken)
                if (input == null)
                {
                    Compiler.Logging.Logger.LogMain("Standard input stream closed. Exiting process.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    System.Threading.Thread.Sleep(50);
                    continue;
                }

                IEnumerable<string> filePaths = ParseInputPaths(input);

#if DEBUG
                if (!filePaths.Any())
                {
                    Compiler.Logging.Logger.LogMain("[DEBUG] No paths from parent process; running test file discovery fallback.");
                    filePaths = DiscoverTestFiles();
                }
#else
                // In non-DEBUG builds, never perform fallback scanning.
                if (!filePaths.Any())
                {
                    Compiler.Logging.Logger.LogMain("No valid files provided by parent process. Waiting for input.");
                    System.Threading.Thread.Sleep(50);
                    continue;
                }
#endif

                // Normalize and filter to only existing absolute files
                var toProcess = new List<string>();
                foreach (var p in filePaths)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    string trimmed = p.Trim();
                    try
                    {
                        if (!Path.IsPathRooted(trimmed))
                        {
                            Compiler.Logging.Logger.LogMain("[WARN] Skipping non-absolute path: " + trimmed);
                            continue;
                        }
                        var full = Path.GetFullPath(trimmed);
                        if (File.Exists(full)) toProcess.Add(full);
                        else Compiler.Logging.Logger.LogMain("[WARN] File not found: " + full);
                    }
                    catch (Exception ex)
                    {
                        Compiler.Logging.Logger.LogMain("[ERROR] Failed to normalize path '" + trimmed + "': " + ex.Message);
                    }
                }

                if (!toProcess.Any())
                {
                    Compiler.Logging.Logger.LogMain("No valid files to process after normalization.");
                    System.Threading.Thread.Sleep(50); // FIX: Prevents 100% CPU spikes if given bad data repetitively 
                    continue;
                }

                try
                {
                    manager.ProcessFiles(toProcess);
                }
                catch (Exception ex)
                {
                    Compiler.Logging.Logger.ReportUnhandledException(ex);
                }
            }
        }

        private static IEnumerable<string> DiscoverTestFiles()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var testDir = Path.Combine(desktop, "marshal-ide-compiler-debbug", "input");

            if (!Directory.Exists(testDir))
            {
                Compiler.Logging.Logger.LogMain("Test directory not found: " + testDir);
                try { Directory.CreateDirectory(testDir); } catch { }
                return Array.Empty<string>();
            }

            var allowedExtensions = new[] { ".decision", ".event", ".focus", ".idea", ".scriptedgui", ".script" };
            var files = new List<string>();

            foreach (var ext in allowedExtensions)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + ext, SearchOption.AllDirectories));
            }

            return files.Select(Path.GetFullPath).ToArray();
        }

        private static IEnumerable<string> ParseInputPaths(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
            var trimmed = input.Trim();

            // If it's a JSON array, prefer parsing it as such
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var el in root.EnumerateArray())
                        {
                            list.Add(el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString());
                        }
                        return list.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                    }
                }
                catch { /* Fall through to literal handling below */ }
            }

            // If it looks like a quoted string, try JSON parse for a valid JSON string first (only double-quote JSON is valid);
            // otherwise just strip surrounding quotes and treat as a single path. This preserves commas inside the path.
            if ((trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) || (trimmed.StartsWith("'") && trimmed.EndsWith("'")))
            {
                if (trimmed.StartsWith("\""))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(trimmed);
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.String)
                        {
                            return new[] { root.GetString() };
                        }
                    }
                    catch { /* ignore and fall back to raw substring */ }
                }

                return new[] { trimmed.Substring(1, trimmed.Length - 2) };
            }

            // If there are commas, we only want to split when the comma-separated parts look like absolute paths.
            // This avoids splitting a single absolute path that contains commas in folder names.
            if (trimmed.Contains(","))
            {
                var parts = trimmed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim().Trim('"', '\''))
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .ToArray();

                if (parts.Length > 1 && parts.All(p => Path.IsPathRooted(p)))
                {
                    return parts;
                }

                // Not a list of rooted paths — treat the entire input as a single path (commas are part of the path)
                return new[] { trimmed };
            }

            return new[] { trimmed };
        }
    }
}