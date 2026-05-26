using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Stopwatch timer = Stopwatch.StartNew();
            // Defaults
            string defaultOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Importing Test Mod", "mod");
            string defaultInputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Importing Test Mod");
            string defaultDebugDir = defaultInputDirectory;

            // Allow overriding via args. Support named (--input=, --output=, --debug=) and positional (input output debug)
            string inputDirectory = defaultInputDirectory;
            string outputDir = defaultOutputDir;
            string debugOutputDir = defaultDebugDir;

            if (args != null && args.Length > 0)
            {
                var positional = new List<string>();
                foreach (var a in args)
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;
                    if (a == "--help" || a == "-h")
                    {
                        Console.WriteLine("Usage: [--input=PATH] [--output=PATH] [--debug=PATH] or positional: <input> <output> <debug>");
                        return;
                    }

                    if (a.StartsWith("--input=", StringComparison.OrdinalIgnoreCase))
                    {
                        inputDirectory = a.Substring("--input=".Length);
                        continue;
                    }
                    if (a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
                    {
                        outputDir = a.Substring("--output=".Length);
                        continue;
                    }
                    if (a.StartsWith("--debug=", StringComparison.OrdinalIgnoreCase))
                    {
                        debugOutputDir = a.Substring("--debug=".Length);
                        continue;
                    }

                    // key=value form without leading dashes
                    var eq = a.IndexOf('=');
                    if (eq > 0)
                    {
                        var key = a.Substring(0, eq);
                        var val = a.Substring(eq + 1);
                        if (key.Equals("input", StringComparison.OrdinalIgnoreCase)) { inputDirectory = val; continue; }
                        if (key.Equals("output", StringComparison.OrdinalIgnoreCase)) { outputDir = val; continue; }
                        if (key.Equals("debug", StringComparison.OrdinalIgnoreCase)) { debugOutputDir = val; continue; }
                    }

                    positional.Add(a);
                }

                if (positional.Count > 0) inputDirectory = positional[0];
                if (positional.Count > 1) outputDir = positional[1];
                if (positional.Count > 2) debugOutputDir = positional[2];

                // Normalize to full paths
                try { inputDirectory = Path.GetFullPath(inputDirectory); } catch { }
                try { outputDir = Path.GetFullPath(outputDir); } catch { }
                try { debugOutputDir = Path.GetFullPath(debugOutputDir); } catch { }
            }
            var spawnedProcesses = new List<Process>();
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                PerformEmergencyShutdown(e.ExceptionObject as Exception, spawnedProcesses, outputDir);
            };
            var context = new ImportContext();
            
            // Create importer instances so we can reference them from compilers later
            var ScriptedEffectImporter = new ScriptedEffectImporter();
            var ScriptedTriggerImporter = new ScriptedTriggerImporter();
            var OnActionsImporter = new OnActionsImporter();
            var GameRulesImporter = new GameRulesImporter();
            var EventsImporter = new EventsImporter();
            var IdeasImporter = new IdeasImporter();
            var LocalisationImporter = new LocalisationImporter();
            var DecisionsImporter = new DecisionsImporter();
            var DecisionCategoriesImporter = new DecisionCategoriesImporter();
            var FocusTreeImporter = new FocusTreeImporter();
            var ScriptedLocalisationImporter = new ScriptedLocalisationImporter();
            var ScriptedGuiImporter = new ScriptedGuiImporter();
            var GFXImporter = new GFXImporter();
            var InterfaceImporter = new InterfaceImporter();
            var importers = new List<BaseImporter>
            {
                LocalisationImporter,
                ScriptedEffectImporter,
                ScriptedTriggerImporter,
                OnActionsImporter,
                GameRulesImporter,
                EventsImporter,
                IdeasImporter,
                DecisionsImporter,
                DecisionCategoriesImporter,
                FocusTreeImporter,
                ScriptedLocalisationImporter,
                ScriptedGuiImporter,
                GFXImporter,
                InterfaceImporter,
            };

            // Initialize logger with explicit debug output path (where Debbug folder will be created) and the input path
            DebugLogger.Initialize(debugOutputDir, inputDirectory);
            DebugLogger.Log("Global", "", LogLevel.Info, "Launching all importers...");

            // Start all importer tasks with minimal overhead (no per-importer Task.Run / try/catch).
            var importerTasks = importers.Select(i => i.RunImportAsync(inputDirectory, context)).ToArray();

            // Run the standalone DDS copier in parallel (it should not be part of the BaseImporter list).
            var ddsCopier = new DDSImporter();
            var ddsTask = ddsCopier.RunAsync(inputDirectory, outputDir);

            // Await all; Task.WhenAll will throw if any task faults. We'll catch once and record per-task errors below
            try
            {
                
                await Task.WhenAll(importerTasks.Append(ddsTask));
            }
            catch (Exception ex)
            {
                await PerformEmergencyShutdown(ex, spawnedProcesses, outputDir);
            }

            // Record any importer failures into the shared StructuralErrors queue but also mark them as critical.
            for (int idx = 0; idx < importers.Count; idx++)
            {
                var task = importerTasks[idx];
                if (task.IsFaulted)
                {
                    var agg = task.Exception?.Flatten();
                    var inner = agg?.InnerException;
                    var message = inner?.Message ?? agg?.Message ?? "(no message)";
                    var entry = $"{importers[idx].GetType().Name} failed: {message}";
                    context.StructuralErrors.Add(entry);
                    context.CriticalErrors.Add(entry);
                }
            }

            

            // --- Compilers ---
            // Create compilers and run them after importers have finished. Compilers may depend
            // on data produced by specific importer instances (we pass the instances in).
            var compilers = new List<Func<Task>>
            {
                async () => await new ScriptedEffectsCompiler(ScriptedEffectImporter).RunCompileAsync(outputDir, context),
                async () => await new ScriptedTriggerCompiler(ScriptedTriggerImporter).RunCompileAsync(outputDir, context),
                async () => await new OnActionsCompiler(OnActionsImporter).RunCompileAsync(outputDir, context),
                async () => await new GameRulesCompiler(GameRulesImporter).RunCompileAsync(outputDir, context),
                async () => await new EventsCompiler(EventsImporter).RunCompileAsync(outputDir, context),
                async () => await new IdeasCompiler(IdeasImporter).RunCompileAsync(outputDir, context),
                async () => await new DecisionsCompiler(DecisionsImporter,DecisionCategoriesImporter).RunCompileAsync(outputDir, context),
                async () => await new FocusTreeCompiler(FocusTreeImporter).RunCompileAsync(outputDir, context),
                async () => await new ScriptedGuiCompiler(ScriptedLocalisationImporter,ScriptedGuiImporter,GFXImporter,InterfaceImporter).RunCompileAsync(outputDir, context),
            };
            DebugLogger.Log("Global", "", LogLevel.Info, "Launching all compilers...");
            // Run compilers and measure per-compiler duration / success
            var compilerResults = new List<(string Name, TimeSpan Duration, bool Success)>();
            var compilerTasks = compilers.Select(async c =>
            {
                var sw = Stopwatch.StartNew();
                string name = c.Method?.DeclaringType?.Name ?? "Compiler";
                try
                {
                    await c();
                    sw.Stop();
                    compilerResults.Add((name, sw.Elapsed, true));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    compilerResults.Add((name, sw.Elapsed, false));
                    context.CriticalErrors.Add($"Compiler {name} failed: {ex.Message}");
                }
            }).ToArray();

            await Task.WhenAll(compilerTasks);
            timer.Stop();

            double totalSeconds = timer.Elapsed.TotalSeconds;
            if (!context.StructuralErrors.IsEmpty)
            {
                foreach (var error in context.StructuralErrors)
                {
                    DebugLogger.AddStructuralError(error);
                    DebugLogger.Log("Global", "", LogLevel.Error, error);
                }
            }

            if (!context.CriticalErrors.IsEmpty)
            {
                foreach (var err in context.CriticalErrors)
                {
                    DebugLogger.AddCriticalError(err);
                    DebugLogger.Log("Global", "", LogLevel.Fatal, err);
                }
            }
            // Print per-importer diagnostics
            DebugLogger.Log("Global", "", LogLevel.Info, "IMPORTER METRICS");
            int totalImportedFiles = 0;
            foreach (var imp in importers)
            {
                totalImportedFiles += imp.ProcessedFileCount;
                double memDeltaMb = (imp.MemoryAfterBytes - imp.MemoryBeforeBytes) / 1024.0 / 1024.0;
                double throughput = imp.ImportDuration.TotalSeconds > 0 ? imp.ProcessedFileCount / imp.ImportDuration.TotalSeconds : imp.ProcessedFileCount;
                DebugLogger.Log(imp.GetType().Name, "", LogLevel.Info, $"Files={imp.ProcessedFileCount}, Time={imp.ImportDuration.TotalSeconds:F2}s, Throughput={throughput:F02} files/s, MemDelta={memDeltaMb:F2} MB");
            }
            DebugLogger.Log("Global", "", LogLevel.Info, "COMPILER METRICS");
            foreach (var cr in compilerResults)
            {
                DebugLogger.Log("Global", "", LogLevel.Info, $"{cr.Name}: Time={cr.Duration.TotalSeconds:F2}s, Success={(cr.Success ? "Yes" : "No")}");
            }
            DebugLogger.Log("Global", "", LogLevel.Info, "PERFORMANCE METRICS");
            DebugLogger.Log("Global", "", LogLevel.Info, $"Total Time: {totalSeconds:F2} seconds");
            DebugLogger.Log("Global", "", LogLevel.Info, $"Total files processed by importers: {totalImportedFiles}");
            DebugLogger.Log("Global", "", LogLevel.Info, "All processing complete.");
            DebugLogger.Log("Global", "", LogLevel.Info, "Exiting...");

            // Write out debug logs to files
            DebugLogger.WriteOut(importers, compilerResults, totalSeconds, totalImportedFiles);

            IPC.Send("ProcessingComplete", "All importers and compilers have completed successfully.");
            //Console.ReadLine();
        }
        static async Task PerformEmergencyShutdown(Exception ex, List<Process> spawnedProcesses, string outputDir)
        {
            IPC.Send("FatalError", ex.Message);
            var exceptionsToLog = new List<Exception>();
            if (ex is AggregateException agg)
            {
                exceptionsToLog.AddRange(agg.Flatten().InnerExceptions);
            }
            else
            {
                exceptionsToLog.Add(ex);
            }

            IPC.Send("FatalErrorInfo", "Building crash report.");
            DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Fatal, $"#FATAL CRASH REPORT");
            DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Fatal, $"- **Timestamp:** {DateTime.Now}");
            DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Fatal, $"## Exceptions Encountered:\n");

            foreach (var e in exceptionsToLog)
            {
                DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Fatal, $"{e.GetType().Name}: {e.Message} **Stack Trace:**\n```csharp\n{e.StackTrace}\n```\n---");
            }

            IPC.Send("FatalErrorInfo", "Shutting down and cleaning up...");
            foreach (var proc in spawnedProcesses.Where(p => !p.HasExited))
            {
                try { proc.Kill(true); } catch { /* Fire and forget */ }
            }
            await CleanupAsync(outputDir);
            await DebugLogger.WriteCrashReportAsync();

            Environment.Exit(1);
        }
        public static async Task CleanupAsync(string outputDir)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(outputDir)) return;

                    var di = new DirectoryInfo(outputDir);

                    // Delete all files in the root of the output dir
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }

                    // Delete all subdirectories recursively
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        // We use true for recursive delete
                        dir.Delete(true);
                    }

                    DebugLogger.Log("Global", "", LogLevel.Info, "Cleanup completed successfully.");
                }
                catch (IOException ioEx)
                {
                    // Usually happens if a file is still locked by an importer or the OS
                    DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Warning, $"Cleanup I/O Warning: {ioEx.Message}");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("CRASH_HANDLER", "", LogLevel.Error, $"Cleanup failed: {ex.Message}");
                }
            });
        }
    }
}

