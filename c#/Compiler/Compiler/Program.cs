using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// COMPILER INPUT FORMAT & OPTIONS
/// ================================
/// 
/// The compiler receives configuration via stdin. All paths must be absolute.
/// Spaces around delimiters are ignored.
/// 
/// MINIMUM REQUIRED (Default compilation, no AST output):
///   C:\absolute\path\to\file.event
/// 
/// WITH AST OUTPUT (Compile normally, then export AST as JSON):
///   C:\absolute\path\to\file.event && out_ast = true && o = C:\absolute\output\path\file.json
/// 
/// AST ONLY (Skip compilation, only export AST as JSON):
///   C:\absolute\path\to\file.event && only_out_ast = true && o = C:\absolute\output\path\file.json
/// 
/// COMMAND-LINE FLAGS (via args):
///   --debug=<path>: Optional. Absolute path to debug logs directory
///   --output=<path>: Optional. Absolute path to output directory (defaults to Desktop/marshal-ide-compiler-debbug/compiled-data)
///   --cachePath=<path>: REQUIRED. Absolute path to cache directory for tracking compiled files
/// 
/// STDIN PARAMETERS:
///   - File path (required): Absolute path to the file to compile
///   - out_ast: If "true", output AST after standard compilation runs
///   - only_out_ast: If "true", skip compilation entirely and only output AST
///   - o: Output path for AST JSON (required if AST output is enabled)
///        Must include filename and extension (e.g., file.json)
/// 
/// Notes:
///   - At most ONE of out_ast or only_out_ast should be set
///   - If AST output fails, an AstOutputFailure IPC message is sent immediately
///   - Results always included in final ValidationReport with ast_output_success field
/// </summary>

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

            string cachePathArg = null;
            if (args != null && args.Length > 0)
            {
                cachePathArg = args.FirstOrDefault(a => a.StartsWith("--cachePath=", StringComparison.OrdinalIgnoreCase));
            }

            string resolvedCachePath;
            if (!string.IsNullOrWhiteSpace(cachePathArg))
            {
                var raw = cachePathArg.Substring("--cachePath=".Length).Trim();
                if (!Path.IsPathRooted(raw))
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] --cachePath must be an absolute path.");
                    return;
                }

                try
                {
                    resolvedCachePath = Path.GetFullPath(raw);
                }
                catch
                {
                    Compiler.Logging.Logger.LogMain("[ERROR] Failed to normalize provided --cachePath path.");
                    return;
                }
            }
            else
            {
                Compiler.Logging.Logger.LogMain("[ERROR] --cachePath is required.");
                return;
            }

            BaseCompiler.OutputPath = resolvedOutputPath;
            CacheHandler.CachePath = resolvedCachePath;
            try { Directory.CreateDirectory(BaseCompiler.OutputPath); } catch { }
            try { Directory.CreateDirectory(CacheHandler.CachePath); } catch { }

            var manager = new ProcessManager();
            IPC.Log("Info", $"Compiler initialized and ready to compile. Received Output path:{resolvedOutputPath}     Cache path:{resolvedCachePath}     Logs path:{resolvedLogPath}");
            Compiler.Logging.Logger.LogMain($"[PROGRAM] Compiler started with Output={resolvedOutputPath} Cache={resolvedCachePath} Logs={resolvedLogPath}");
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

                Compiler.Logging.Logger.LogMain($"[PROGRAM] Raw input received: {input}");

                // Parse input into file paths and compilation options
                var parsedRequests = ParseCompilationRequests(input);

#if DEBUG
                if (!parsedRequests.Any())
                {
                    Compiler.Logging.Logger.LogMain("[DEBUG] No paths from parent process; running test file discovery fallback.");
                    var testFiles = DiscoverTestFiles();
                    parsedRequests = testFiles.Select(f => CompilationOptions.Default(f)).ToList();
                }
#else
                // In non-DEBUG builds, never perform fallback scanning.
                if (!parsedRequests.Any())
                {
                    Compiler.Logging.Logger.LogMain("No valid files provided by parent process. Waiting for input.");
                    System.Threading.Thread.Sleep(50);
                    continue;
                }
#endif

                // Normalize and filter to only existing absolute files
                var toProcess = new List<CompilationOptions>();
                foreach (var request in parsedRequests)
                {
                    if (string.IsNullOrWhiteSpace(request.FilePath)) continue;
                    string trimmed = request.FilePath.Trim();
                    try
                    {
                        if (!Path.IsPathRooted(trimmed))
                        {
                            Compiler.Logging.Logger.LogMain("[WARN] Skipping non-absolute path: " + trimmed);
                            continue;
                        }

                        string normalized = Path.GetFullPath(trimmed);
                        if (!File.Exists(normalized))
                        {
                            Compiler.Logging.Logger.LogMain("[WARN] Skipping non-existent file: " + normalized);
                            continue;
                        }

                        // Update the FilePath to the normalized path
                        request.FilePath = normalized;
                        toProcess.Add(request);
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

                Compiler.Logging.Logger.LogMain($"[PROGRAM] Total files to process: {toProcess.Count}");
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

            var allowedExtensions = new[] { ".decision", ".event", ".focus", ".idea", ".scriptedgui", ".script", ".equipment", ".unit", ".history" };
            var files = new List<string>();

            foreach (var ext in allowedExtensions)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + ext, SearchOption.AllDirectories));
            }

            return files.Select(Path.GetFullPath).ToArray();
        }

        /// <summary>
        /// Parses compilation requests from input string.
        /// Format: "file_path" or "file_path && out_ast = true && o = output_path" or "file_path && only_out_ast = true && o = output_path"
        /// </summary>
        private static List<CompilationOptions> ParseCompilationRequests(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<CompilationOptions>();

            var results = new List<CompilationOptions>();

            // Split by && to separate file path from options
            string[] parts = input.Split(new[] { "&&" }, StringSplitOptions.None);

            if (parts.Length == 0)
                return results;

            // First part is the file path
            string filePath = parts[0].Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(filePath))
                return results;

            // Parse remaining parts for options
            bool outputAst = false;
            bool onlyOutputAst = false;
            string astOutputPath = null;

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                // Check for out_ast = true
                if (part.IndexOf("out_ast", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (part.IndexOf("=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] keyVal = part.Split(new[] { "=" }, StringSplitOptions.None);
                        if (keyVal.Length == 2)
                        {
                            string value = keyVal[1].Trim().ToLowerInvariant();
                            if (value == "true")
                                outputAst = true;
                        }
                    }
                }

                // Check for only_out_ast = true
                if (part.IndexOf("only_out_ast", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (part.IndexOf("=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] keyVal = part.Split(new[] { "=" }, StringSplitOptions.None);
                        if (keyVal.Length >= 2)
                        {
                            string value = keyVal[1].Trim().ToLowerInvariant();
                            if (value == "true")
                                onlyOutputAst = true;
                        }
                    }
                }

                // Check for o = output_path (capture everything after = as the path)
                if (part.IndexOf(" o ", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    part.StartsWith("o ", StringComparison.OrdinalIgnoreCase))
                {
                    // Find the position of '='
                    int eqPos = part.IndexOf("=", StringComparison.OrdinalIgnoreCase);
                    if (eqPos >= 0)
                    {
                        astOutputPath = part.Substring(eqPos + 1).Trim().Trim('"', '\'');
                    }
                }
            }

            // Create the compilation options
            CompilationOptions options;
            if (outputAst || onlyOutputAst)
            {
                options = CompilationOptions.WithAstOutput(filePath, astOutputPath, onlyOutputAst);
            }
            else
            {
                options = CompilationOptions.Default(filePath);
            }

            results.Add(options);
            return results;
        }
    }
}
