using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class GameRulesCompiler
    {
        private readonly GameRulesImporter _importer;

        public GameRulesCompiler(GameRulesImporter importer)
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
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"No game rules to compile.");
                return;
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Starting game rules compilation. Found {results.Count} game rules.");

            var scriptsFolder = Path.Combine(rootDirectory, "mod", "scripts");
            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Ensuring scripts folder exists at: {scriptsFolder}");
            Directory.CreateDirectory(scriptsFolder);

            // Group by the saved FileName so we create one .script per source file
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

                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} game rules)");

                var lines = new List<string>();

                foreach (var rule in group)
                {
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Adding game rule '{rule.Id}' ({rule.Options.Count} options)");

                    // 1. Write the Game Rule Header
                    lines.Add($"game rule {rule.Id}");

                    // 2. Resolve and write Name
                    string nameLoc = ResolveLoc(context, rule.NameLocKey);
                    lines.Add($"    name \"{nameLoc}\"");

                    // 3. Resolve and write Group (if it exists)
                    if (!string.IsNullOrEmpty(rule.GroupLocKey))
                    {
                        string groupLoc = ResolveLoc(context, rule.GroupLocKey);
                        lines.Add($"    group \"{groupLoc}\"");
                    }

                    // 4. Process Options
                    foreach (var option in rule.Options)
                    {
                        string optionLoc = ResolveLoc(context, option.NameLocKey);
                        string optionPrefix = option.IsDefault ? "default option" : "option";

                        // Write the option header (e.g. default option "enable")
                        lines.Add($"    {optionPrefix} \"{optionLoc}\"");

                        // Write the raw scripted logic inside the option
                        foreach (var l in option.Lines)
                        {
                            // Skip the closing brace for the option block itself
                            // (Since the custom script format doesn't use '}' for options)
                            if (l.Content == "}" && l.Depth == 1)
                                continue;

                            // Create indentation: 4 spaces per depth level
                            var indent = new string(' ', Math.Max(0, l.Depth * 4));
                            lines.Add(indent + l.Content);
                        }
                    }

                    lines.Add(string.Empty); // Blank line between game rules
                }

                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Queuing write of {outPath} ({lines.Count} lines)");
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
                DebugLogger.Log("Compiler", "", LogLevel.Error, $"Exception while writing .script files: {ex.Message}");
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"game rules compilation complete");
        }

        // Helper method to look up strings in the Localisation context
        private string ResolveLoc(ImportContext context, string locKey)
        {
            if (string.IsNullOrWhiteSpace(locKey)) return "";

            // Assuming standard "english" localisation. You can change this if needed.
            if (context.Languages.TryGetValue("english", out var engDict))
            {
                if (engDict.TryGetValue(locKey, out var translated))
                {
                    // Return translated string without extra quotes (we wrap it manually in the compiler)
                    return translated.Trim('\"');
                }
            }

            // Fallback to the raw key if no localisation was found
            return locKey;
        }
    }
}