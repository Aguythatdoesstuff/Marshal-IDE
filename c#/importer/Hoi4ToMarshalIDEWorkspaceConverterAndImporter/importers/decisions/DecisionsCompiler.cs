using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace importer
{
    public class DecisionsCompiler
    {
        private readonly DecisionsImporter _decisionsImporter;
        private readonly DecisionCategoriesImporter _categoriesImporter;

        public DecisionsCompiler(DecisionsImporter decisionsImporter, DecisionCategoriesImporter categoriesImporter)
        {
            _decisionsImporter = decisionsImporter ?? throw new ArgumentNullException(nameof(decisionsImporter));
            _categoriesImporter = categoriesImporter ?? throw new ArgumentNullException(nameof(categoriesImporter));
        }

        public async Task RunCompileAsync(string rootDirectory, ImportContext context)
        {
            var decisions = _decisionsImporter.Results;
            var categories = _categoriesImporter.Results;

            if ((decisions == null || decisions.Count == 0) && (categories == null || categories.Count == 0))
            {
                DebugLogger.Log("DecisionsCompiler", "", LogLevel.Info, $"No decisions or categories to compile.");
                return;
            }

            var outFolder = Path.Combine(rootDirectory, "mod", "decisions");
             DebugLogger.Log("DecisionsCompiler", "", LogLevel.Info, $"Ensuring decisions folder exists at: {outFolder}");
            Directory.CreateDirectory(outFolder);

            // Group decisions by category id
            var decisionsByCategory = decisions.GroupBy(d => d.Category ?? "__unknown").ToDictionary(g => g.Key, g => g.ToList());

            var writeTasks = new List<Task>();
            var outPaths = new List<string>();

            // Ensure we iterate over all known categories, even if no decisions were found for them
            var allCategoryIds = new HashSet<string>(categories.Select(c => c.Id));
            foreach (var d in decisions) if (!string.IsNullOrEmpty(d.Category)) allCategoryIds.Add(d.Category);

            foreach (var catId in allCategoryIds)
            {
                var category = categories.FirstOrDefault(c => c.Id == catId);
                var catDecisions = decisionsByCategory.ContainsKey(catId) ? decisionsByCategory[catId] : new List<DecisionsImporter.Decision>();

                var outFileName = catId + ".decision";
                var outPath = Path.Combine(outFolder, outFileName);
                if (File.Exists(outPath))
                {
                    int suffix = 1;
                    string candidate;
                    do
                    {
                        candidate = $"{catId}_{suffix}.decision";
                        outPath = Path.Combine(outFolder, candidate);
                        suffix++;
                    } while (File.Exists(outPath));
                     DebugLogger.Log("DecisionsCompiler", "", LogLevel.Warning, $"Output file '{outFileName}' already exists. Using '{Path.GetFileName(outPath)}' instead.");
                }

                 DebugLogger.Log("DecisionsCompiler", "", LogLevel.Info, $"Compiling category '{catId}' -> {outPath} ({catDecisions.Count} decisions)");

                var lines = new List<string>();

                // Category header
                lines.Add($"category {catId}");

                // Category name/desc from localisation if available
                if (category != null)
                {
                    var name = ResolveLoc(context, category.NameLocKey);
                    lines.Add($"    name \"{name}\"");
                    if (!string.IsNullOrEmpty(category.DescLocKey))
                    {
                        var desc = ResolveLoc(context, category.DescLocKey);
                        lines.Add($"    desc \"{desc}\"");
                    }
                    if (!string.IsNullOrEmpty(category.Sprite))
                    {
                        lines.Add($"    sprite \"{category.Sprite}\"");
                    }
                    if (!string.IsNullOrEmpty(category.priority))
                    {
                        lines.Add($"    priority {category.priority}");
                    }

                    // Dump any raw category lines (adjust indentation according to their depth)
                    foreach (var l in category.Lines)
                    {
                        var indent = new string(' ', Math.Max(0, l.Depth * 4));
                        lines.Add(indent + l.Content);
                    }
                }

                // Decisions inside the category
                foreach (var dec in catDecisions)
                {
                    lines.Add(string.Empty);
                    lines.Add($"    decision {dec.Id}");

                    var dname = ResolveLoc(context, dec.NameLocKey);
                    lines.Add($"        name \"{dname}\"");
                    if (!string.IsNullOrEmpty(dec.DescLocKey))
                    {
                        var ddesc = ResolveLoc(context, dec.DescLocKey);
                        lines.Add($"        desc \"{ddesc}\"");
                    }
                    if (!string.IsNullOrEmpty(dec.Sprite))
                    {
                        lines.Add($"        sprite \"{dec.Sprite}\"");
                    }
                    if (!string.IsNullOrEmpty(dec.priority))
                    {
                        lines.Add($"        priority {dec.priority}");
                    }
                    if (!string.IsNullOrEmpty(dec.cost))
                    {
                        lines.Add($"        cost {dec.cost}");
                    }

                    // Dump raw decision lines
                    foreach (var l in dec.Lines)
                    {
                        var indent = new string(' ', Math.Max(0, l.Depth * 4));
                        lines.Add(indent + l.Content);
                    }
                }

                lines.Add(string.Empty);

                writeTasks.Add(File.WriteAllLinesAsync(outPath, lines, Encoding.UTF8));
                outPaths.Add(outPath);
            }

            try
            {
                await Task.WhenAll(writeTasks);
                
                DebugLogger.Log("DecisionsCompiler", "", LogLevel.Info, $"Successfully wrote {outPaths.Count} .decision files:");
                foreach (var p in outPaths) 
                    DebugLogger.Log("DecisionsCompiler", "", LogLevel.Info, $"  - {p}");
            }
            catch (Exception ex)
            {
                 DebugLogger.Log("DecisionsCompiler", "", LogLevel.Error, $"Exception while writing .decision files: {ex.Message}");
            }
        }

        private string ResolveLoc(ImportContext context, string locKey)
        {
            if (string.IsNullOrWhiteSpace(locKey)) return "";
            string language = "english";
            if (context.Languages.TryGetValue(language, out var engDict))
            {
                if (engDict.TryGetValue(locKey, out var translated))
                {
                    return translated.Trim('"');
                }
            }
            DebugLogger.Log("DecisionsCompiler", "", LogLevel.Warning, $"Missing localisation for key '{locKey}' in '{language}'. Using key as fallback.");
            return locKey;
        }
    }
}