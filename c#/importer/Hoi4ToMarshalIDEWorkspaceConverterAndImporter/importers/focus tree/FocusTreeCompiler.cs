using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class FocusTreeCompiler
    {
        private readonly FocusTreeImporter _importer;

        public FocusTreeCompiler(FocusTreeImporter importer)
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
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"No focus to compile.");
                return;
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Starting focus compilation. Found {results.Count} focus.");

            var focusFolder = Path.Combine(rootDirectory, "mod", "focuses");
            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Ensuring focus folder exists at: {focusFolder}");
            Directory.CreateDirectory(focusFolder);

            // Group by the saved FileName so we create one .focus per source file
            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            DebugLogger.Log("Compiler", "", LogLevel.Info, $"[Compiler] Found {groups.Count()} source file groups to compile.");

            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            foreach (var group in groups)
            {
                var sourceFileName = group.Key;
                var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
                var outFileName = baseName + ".focus";
                var outPath = Path.Combine(focusFolder, outFileName);

                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{baseName}_{suffix}.focus";
                        outPath = Path.Combine(focusFolder, candidate);
                        suffix++;
                    } while (File.Exists(outPath));

                    DebugLogger.Log("Compiler", "", LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }

                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} focuses)");

                var lines = new List<string>();

                foreach (var even in group)
                {
                    if (even.Options != null && even.Options.Count > 0)
                    {
                        DebugLogger.Log("Compiler", "", LogLevel.Info, $"Adding focus '{even.Id}' ({even.Options.Count} options)");
                    }
                    else
                    {
                        DebugLogger.Log("Compiler", "", LogLevel.Info, $"Adding focus '{even.Id}'");
                    }

                    // 1. Write the focus Header
                    if (even.IsDefault)
                    {
                        lines.Add($"default tree {even.Id}");
                    }
                    else
                    {
                        lines.Add($"tree {even.Id} for {even.Tag}");
                    }

                    // 2. Resolve and write Name
                    if (!string.IsNullOrEmpty(even.NameLocKey))
                    {
                        string nameLoc = ResolveLoc(context, even.NameLocKey);
                        lines.Add($"    name \"{nameLoc}\"");
                    }
                    // 3. Resolve and write Group (if it exists)
                    if (!string.IsNullOrEmpty(even.DescLocKey))
                    {
                        string DescLoc = ResolveLoc(context, even.DescLocKey);
                        lines.Add($"    desc \"{DescLoc}\"");
                    }


                    foreach (var l in even.Lines)
                    {
                        // For root/raw lines we preserve all content (including closing braces)
                        // Create indentation: 4 spaces per depth level
                        var indent = new string(' ', Math.Max(0, l.Depth * 4));
                        lines.Add(indent + l.Content);
                    }
                    // 4. Process Options
                    foreach (var option in even.Options)
                    {
                        string optionLoc = ResolveLoc(context, option.NameLocKey);

                        // Write the option header (e.g. default option "enable")
                        lines.Add($"    {"focus"} {option.Id} takes {option.CostDays} days");
                        if (!string.IsNullOrEmpty(option.Id))
                        {
                            string NameLoc = ResolveLoc(context, option.Id);
                            lines.Add($"        name \"{NameLoc}\"");
                        }
                        if (!string.IsNullOrEmpty(option.Id+ "_desc"))
                        {
                            string DescLoc = ResolveLoc(context, option.Id+"_desc");
                            lines.Add($"        desc \"{DescLoc}\"");
                        }
                        if (!string.IsNullOrEmpty(option.Sprite))
                        {
                            lines.Add($"        sprite \"{option.Sprite}\"");
                        }
                        // Write the raw focused logic inside the option
                        foreach (var l in option.Lines)
                        {
                            // Skip the closing brace for the option block itself
                            // (Since the custom focus format doesn't use '}' for options)
                            if (l.Content == "}" && l.Depth == 1)
                                continue;

                            // Create indentation: 4 spaces per depth level
                            var indent = new string(' ', Math.Max(0, l.Depth * 4));
                            lines.Add(indent + l.Content);
                        }
                    }

                    lines.Add(string.Empty); // Blank line between focus
                }

                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Queuing write of {outPath} ({lines.Count} lines)e");
                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .focus files:");
                foreach (var p in outPaths)
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"\"  - {{p");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Compiler", "", LogLevel.Error, $"Exception while writing .focus files: {ex.Message}");
            }

            DebugLogger.Log("Compiler", "", LogLevel.Info, $"focus compilation complete.");
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
            DebugLogger.Log("Compiler", "", LogLevel.Warning, $"Missing localisation for key '{locKey}' in '{language}'. Using key as fallback.e");
            return locKey;
        }
    }
}