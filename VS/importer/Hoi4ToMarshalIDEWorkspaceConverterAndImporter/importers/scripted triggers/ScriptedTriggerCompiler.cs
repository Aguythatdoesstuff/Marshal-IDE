using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    internal class ScriptedTriggerCompiler
    {
        private readonly ScriptedTriggerImporter _importer;

        public ScriptedTriggerCompiler(ScriptedTriggerImporter importer)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            if (_importer == null)
            {
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"No importer provided. Aborting compilation.");
                return;
            }

            var results = _importer.Results;
            if (results == null || results.Count == 0)
            {
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"No scripted triggers to compile.");
                return;
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Starting scripted triggers compilation. Found {results.Count} triggers.");

            var scriptsFolder = Path.Combine(rootDirectory, "mod", "scripts");
            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Ensuring scripts folder exists at: {scriptsFolder}");
            Directory.CreateDirectory(scriptsFolder);

            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Found {groups.Count()} source file groups to compile.");

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

                    DebugLogger.Log("Compiler", "", LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }

                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} triggers)");

                var lines = new List<string>();

                foreach (var trg in group)
                {
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Adding trigger '{trg.Name}' ({trg.Lines.Count} saved lines)");
                    lines.Add($"scripted trigger {trg.Name}");
                    foreach (var l in trg.Lines)
                    {
                        var indent = new string('\t', Math.Max(0, l.Depth));
                        if (l.Content == "}" && l.Depth == 0)
                            continue;
                        lines.Add(indent + l.Content);
                    }

                    lines.Add(string.Empty);
                }

                DebugLogger.Log("Compiler", "", LogLevel.Info, $" Queuing write of {outPath} ({lines.Count} lines)");
                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .script files:");
                foreach (var p in outPaths) 
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"  - {p}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Compiler", "", LogLevel.Error, $"Exception while writing .script files: {{ex.Message");
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Scripted triggers compilation complete.");
        }
    }
}
