using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class IdeasCompiler
    {
        private readonly IdeasImporter _importer;

        public IdeasCompiler(IdeasImporter importer)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            if (_importer == null)
            {
                DebugLogger.Log("IdeasCompiler", "", LogLevel.Error, "No importer provided. Aborting compilation.");
                return;
            }

            var results = _importer.Results;
            if (results == null || results.Count == 0)
            {
                DebugLogger.Log("IdeasCompiler", "", LogLevel.Info, "No ideas to compile.");
                return;
            }

            DebugLogger.Log("IdeasCompiler", "", LogLevel.Info, $"Starting ideas compilation. Found {results.Count} ideas.");

            var ideasFolder = Path.Combine(rootDirectory, "mod", "ideas");
            DebugLogger.Log("IdeasCompiler", ideasFolder, LogLevel.Info, $"Ensuring ideas folder exists at: {ideasFolder}");
            Directory.CreateDirectory(ideasFolder);

            // Group by the saved FileName so we create one .idea per source file
            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            DebugLogger.Log("IdeasCompiler", "", LogLevel.Info, $"Found {groups.Count()} source file groups to compile.");

            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            foreach (var group in groups)
            {
                var sourceFileName = group.Key;
                var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
                var outFileName = baseName + ".idea";
                var outPath = Path.Combine(ideasFolder, outFileName);

                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{baseName}_{suffix}.idea";
                        outPath = Path.Combine(ideasFolder, candidate);
                        suffix++;
                    } while (File.Exists(outPath));

                    DebugLogger.Log("IdeasCompiler", outPath, LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }
                DebugLogger.Log("IdeasCompiler", sourceFileName, LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} ideas)");

                var lines = new List<string>();

                foreach (var idea in group)
                {
                    DebugLogger.Log("IdeasCompiler", sourceFileName, LogLevel.Info, $"Adding idea '{idea.Id}'");

                    // 1. Write the idea Header
                    lines.Add($"{idea.Category} idea {idea.Id}");

                    // 2. Resolve and write Name
                    string nameLoc = ResolveLoc(context, idea.NameLocKey);
                    lines.Add($"    name \"{nameLoc}\"");

                    // 3. Resolve and write Group (if it exists)
                    if (!string.IsNullOrEmpty(idea.DescLocKey))
                    {
                        string DescLoc = ResolveLoc(context, idea.DescLocKey);
                        lines.Add($"    desc \"{DescLoc}\"");
                    }


                    // Write the raw ideaed logic inside the option
                    foreach (var l in idea.Lines)
                    {
                        // Create indentation: 4 spaces per depth level
                        var indent = new string(' ', Math.Max(0, (l.Depth-2) * 4)); // minus 2 Depth due to the 2 wrappers we dont need that are in vanilla
                        lines.Add(indent + l.Content);
                    }
                    

                    lines.Add(string.Empty); // Blank line between ideas
                }

                DebugLogger.Log("IdeasCompiler", outPath, LogLevel.Info, $"Queuing write of {outPath} ({lines.Count} lines)");
                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                DebugLogger.Log("IdeasCompiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .idea files:");
                foreach (var p in outPaths) DebugLogger.Log("IdeasCompiler", p, LogLevel.Info, p);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("IdeasCompiler", "", LogLevel.Error, $"Exception while writing .idea files: {ex.Message}");
            }

            DebugLogger.Log("IdeasCompiler", "", LogLevel.Info, "ideas compilation complete.");
        }

        // Helper method to look up strings in the Localisation context
        private string ResolveLoc(ImportContext context, string locKey)
        {
            if (string.IsNullOrWhiteSpace(locKey)) return "";
            string language = "english";
            // Assuming standard "english" localisation. You can change this if needed.
            if (context.Languages.TryGetValue(language, out var engDict))
            {
                if (engDict.TryGetValue(locKey, out var translated))
                {
                    // Return translated string without extra quotes (we wrap it manually in the compiler)
                    return translated.Trim('\"');
                }
            }

            // Fallback to the raw key if no localisation was found
            DebugLogger.Log("IdeasCompiler", locKey, LogLevel.Warning, $"Missing localisation for key '{locKey}' in '{language}'. Using key as fallback.");
            return locKey;
        }
    }
}