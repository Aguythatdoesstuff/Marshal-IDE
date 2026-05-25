using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class EventsCompiler
    {
        private readonly EventsImporter _importer;

        public EventsCompiler(EventsImporter importer)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            if (_importer == null)
            {
                DebugLogger.Log("EventsCompiler", "", LogLevel.Error, "No importer provided. Aborting compilation.");
                return;
            }

            var results = _importer.Results;
            if (results == null || results.Count == 0)
            {
                DebugLogger.Log("EventsCompiler", "", LogLevel.Info, "No events to compile.");
                return;
            }

            DebugLogger.Log("EventsCompiler", "", LogLevel.Info, $"Starting events compilation. Found {results.Count} events.");

            var eventsFolder = Path.Combine(rootDirectory, "mod", "events");
            DebugLogger.Log("EventsCompiler", eventsFolder, LogLevel.Info, $"Ensuring events folder exists at: {eventsFolder}");
            Directory.CreateDirectory(eventsFolder);

            // Group by the saved FileName so we create one .event per source file
            var groups = results.GroupBy(r => r.FileName ?? "unnamed");
            DebugLogger.Log("EventsCompiler", "", LogLevel.Info, $"Found {groups.Count()} source file groups to compile.");

            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            foreach (var group in groups)
            {
                var sourceFileName = group.Key;
                var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
                var outFileName = baseName + ".event";
                var outPath = Path.Combine(eventsFolder, outFileName);

                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{baseName}_{suffix}.event";
                        outPath = Path.Combine(eventsFolder, candidate);
                        suffix++;
                    } while (File.Exists(outPath));

                    DebugLogger.Log("EventsCompiler", outPath, LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }

                DebugLogger.Log("EventsCompiler", sourceFileName, LogLevel.Info, $"Processing group for source file '{sourceFileName}' -> output '{outFileName}' ({group.Count()} events)");

                var lines = new List<string>();

                foreach (var even in group)
                {
                    DebugLogger.Log("EventsCompiler", sourceFileName, LogLevel.Info, $"Adding event '{even.Id}' ({even.Options.Count} options)");

                    // 1. Write the event Header
                    lines.Add($"{even.Type} event {even.Id}");

                    // 2. Resolve and write Name
                    string nameLoc = ResolveLoc(context, even.NameLocKey);
                    lines.Add($"    name \"{nameLoc}\"");

                    // 3. Resolve and write Group (if it exists)
                    if (!string.IsNullOrEmpty(even.DescLocKey))
                    {
                        string DescLoc = ResolveLoc(context, even.DescLocKey);
                        lines.Add($"    desc \"{DescLoc}\"");
                    }

                    if (!string.IsNullOrEmpty(even.Sprite))
                    {
                        lines.Add($"    sprite \"{even.Sprite}\"");
                    }
                    
                    // 4. Process Options
                    foreach (var option in even.Options)
                    {
                        string optionLoc = ResolveLoc(context, option.NameLocKey);

                        // Write the option header (e.g. default option "enable")
                        lines.Add($"    {"option"} \"{optionLoc}\"");

                        // Write the raw evented logic inside the option
                        foreach (var l in option.Lines)
                        {
                            // Skip the closing brace for the option block itself
                            // (Since the custom event format doesn't use '}' for options)
                            if (l.Content == "}" && l.Depth == 1)
                                continue;

                            // Create indentation: 4 spaces per depth level
                            var indent = new string(' ', Math.Max(0, l.Depth * 4));
                            lines.Add(indent + l.Content);
                        }
                    }

                    lines.Add(string.Empty); // Blank line between events
                }

                DebugLogger.Log("EventsCompiler", outPath, LogLevel.Info, $"Queuing write of {outPath} ({lines.Count} lines)");
                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                DebugLogger.Log("EventsCompiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .event files:");
                foreach (var p in outPaths) DebugLogger.Log("EventsCompiler", p, LogLevel.Info, p);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("EventsCompiler", "", LogLevel.Error, $"Exception while writing .event files: {ex.Message}");
            }

            DebugLogger.Log("EventsCompiler", "", LogLevel.Info, "events compilation complete.");
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
            DebugLogger.Log("EventsCompiler", locKey, LogLevel.Warning, $"Missing localisation for key '{locKey}' in '{language}'. Using key as fallback.");
            return locKey;
        }
    }
}