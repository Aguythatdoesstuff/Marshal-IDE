   using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class OnActionsCompiler
    {
        private readonly OnActionsImporter _importer;

        public OnActionsCompiler(OnActionsImporter importer)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            if (_importer == null)
            {
                DebugLogger.Log("OnActionsCompiler", "", LogLevel.Error, "No importer provided. Aborting compilation.");
                return;
            }

            var results = _importer.Results;
            if (results == null || results.Count == 0)
            {
                DebugLogger.Log("OnActionsCompiler", "", LogLevel.Info, "No on_actions to compile.");
                return;
            }

            DebugLogger.Log("OnActionsCompiler", "", LogLevel.Info, $"Starting on_actions compilation. Found {results.Count} items.");

            var scriptsFolder = Path.Combine(rootDirectory, "mod", "scripts");
            DebugLogger.Log("OnActionsCompiler", scriptsFolder, LogLevel.Info, $"Ensuring scripts folder exists at: {scriptsFolder}");
            Directory.CreateDirectory(scriptsFolder);

            // Group by the saved FileName so we create one .script per source file
            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            DebugLogger.Log("OnActionsCompiler", "", LogLevel.Info, $"Found {groups.Count()} source file groups to compile.");

            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            foreach (var group in groups)
            {
                var sourceFileName = group.Key;
                var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
                var outFileName = baseName + ".script";
                var outPath = Path.Combine(scriptsFolder, outFileName);

                // If a file with the same name already exists, increment a numeric suffix
                // until we find a free name. Print a warning when this happens.
                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{baseName}_{suffix}.script";
                        outPath = Path.Combine(scriptsFolder, candidate);
                        suffix++;
                    } while (File.Exists(outPath));

                    DebugLogger.Log("OnActionsCompiler", outPath, LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }

                DebugLogger.Log("OnActionsCompiler", sourceFileName, LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} effects)");

                var lines = new List<string>();

                foreach (var effect in group)
                {
                    DebugLogger.Log("OnActionsCompiler", sourceFileName, LogLevel.Info, $"Adding action block ({effect.Lines.Count} saved lines)");
                    // Optional header to indicate which effect we're writing
                    lines.Add($"on action");
                    foreach (var l in effect.Lines)
                    {
                        // Reconstruct indentation using tabs based on recorded depth
                        var indent = new string('\t', Math.Max(0, l.Depth));

                        // Skip the root-level closing brace for the scripted effect. The importer
                        // records a closing '}' at depth 0 but we don't emit an opening '{'
                        // for the effect header (`scripted effect NAME`), so writing this
                        // would produce an unmatched extra '}' after each effect.
                        if (l.Content == "}" && l.Depth == 0)
                            continue;

                        lines.Add(indent + l.Content);
                    }

                    lines.Add(string.Empty); // blank line between effects so it looks neater
                }

                DebugLogger.Log("OnActionsCompiler", outPath, LogLevel.Info, $"Queuing write of {outPath} ({lines.Count} lines)");
                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                DebugLogger.Log("OnActionsCompiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .script files:");
                foreach (var p in outPaths) DebugLogger.Log("OnActionsCompiler", p, LogLevel.Info, p);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("OnActionsCompiler", "", LogLevel.Error, $"Exception while writing .script files: {ex.Message}");
                // Optionally rethrow or continue; we'll log and continue
            }
            DebugLogger.Log("OnActionsCompiler", "", LogLevel.Info, "on_actions compilation complete.");
        }
    }
}
