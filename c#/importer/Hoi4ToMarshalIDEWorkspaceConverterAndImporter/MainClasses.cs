using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace importer
{
    public class ImportContext
    {
        // Languages -> (key -> value)
        public ConcurrentDictionary<string, ConcurrentDictionary<string, string>> Languages { get; } =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        // Warnings produced by importers (eg. duplicate ids)
        public ConcurrentBag<string> LocalisationWarnings { get; } = new ConcurrentBag<string>();

        // This is the "Signal". Importers that need Loc will await this Task.
        public TaskCompletionSource<bool> LocalisationReady { get; } = new TaskCompletionSource<bool>();
        // Structural errors found while parsing files (unbalanced braces, etc.)
        public ConcurrentBag<string> StructuralErrors { get; } = new ConcurrentBag<string>();

        // Critical/fatal errors: importer or top-level Task.WhenAll failures should be recorded here
        public ConcurrentBag<string> CriticalErrors { get; } = new ConcurrentBag<string>();
    }

    public abstract class BaseImporter
    {
        public abstract string FolderSubPath { get; }

        // Per-importer file extensions. Override in derived importers to change which files are processed.
        public virtual IEnumerable<string> FileExtensions => new[] { ".txt" };

        // Add this line to control recursive vs top-level searching. Defaults to recursive.
        public virtual SearchOption DirectorySearchOption => SearchOption.AllDirectories;

        public virtual bool RequiresLocalisation => false;
        protected ImportContext Context { get; private set; }
        // Number of files this importer processed during the last RunImportAsync
        private int _processedFileCount;
        public int ProcessedFileCount => _processedFileCount;

        // Timing and memory info for diagnostics
        public TimeSpan ImportDuration { get; private set; }
        public long MemoryBeforeBytes { get; private set; }
        public long MemoryAfterBytes { get; private set; }

        public async Task RunImportAsync(string rootDirectory, ImportContext context)
        {

            this.Context = context;
            var sw = Stopwatch.StartNew();
            MemoryBeforeBytes = GC.GetTotalMemory(false);
            string finalPath = Path.Combine(rootDirectory, FolderSubPath);

            DebugLogger.Log(this.GetType().Name, finalPath, LogLevel.Info, $"[System] {this.GetType().Name} is looking in: {finalPath}");

            if (!Directory.Exists(finalPath))
            {
                DebugLogger.Log(this.GetType().Name, finalPath, LogLevel.Warning, $"[Warning] Folder NOT FOUND: {finalPath}. skipping {this.GetType().Name}.");
                if (this is LocalisationImporter) Context.LocalisationReady.TrySetResult(true);
                return;
            }

            // Build search patterns from the FileExtensions provided by the importer.
            var extensions = FileExtensions?.ToList() ?? new List<string> { ".txt" };
            var patterns = extensions.Select(e => e.StartsWith('.') ? $"*{e}" : $"*.{e}").ToList();

            // UPDATE HERE: Replace SearchOption.AllDirectories with the DirectorySearchOption property
            var files = patterns
                .SelectMany(p => Directory.EnumerateFiles(finalPath, p, DirectorySearchOption))
                .Distinct()
                .ToList();

            DebugLogger.Log(this.GetType().Name, finalPath, LogLevel.Info, $"[System] {this.GetType().Name} found {files.Count} files in {FolderSubPath}");

            _processedFileCount = 0;
            var tasks = files.Select(file => Task.Run(async () =>
            {
                await ProcessFileAsync(file);
                System.Threading.Interlocked.Increment(ref _processedFileCount);
            })).ToList();

            await Task.WhenAll(tasks);

            sw.Stop();
            ImportDuration = sw.Elapsed;
            MemoryAfterBytes = GC.GetTotalMemory(false);

            if (this is LocalisationImporter) Context.LocalisationReady.TrySetResult(true);
        }

        private async Task ProcessFileAsync(string filePath)
        {
            await Task.Run(async () =>
            {
                using var reader = new StreamReader(filePath);
                int depth = 0;
                string line;
                string fileName = Path.GetFullPath(filePath);

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    
                    string sanitized = line.Replace("\u200B", "")
                          .Replace("\uFEFF", "")
                          .Replace("\u00A0", " ");

                    string processingLine = sanitized;
                    int hashIndex = processingLine.IndexOf('#');
                    if (hashIndex >= 0)
                    {
                        processingLine = processingLine.Substring(0, hashIndex);
                    }

                    string trimmed = processingLine.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

                    // Local helper: count braces outside quoted segments (ignore braces inside "...")
                    static (int open, int close) CountBracesOutsideQuotes(string s)
                    {
                        int open = 0, close = 0;
                        bool inQuote = false;
                        char quoteChar = '\0';
                        for (int i = 0; i < s.Length; i++)
                        {
                            char c = s[i];
                            if ((c == '"' || c == '\'') )
                            {
                                // toggle quote state if not escaped
                                bool escaped = i > 0 && s[i - 1] == '\\';
                                if (!escaped)
                                {
                                    if (!inQuote)
                                    {
                                        inQuote = true;
                                        quoteChar = c;
                                    }
                                    else if (quoteChar == c)
                                    {
                                        inQuote = false;
                                        quoteChar = '\0';
                                    }
                                }
                            }

                            if (inQuote) continue;

                            if (c == '{') open++;
                            else if (c == '}') close++;
                        }
                        return (open, close);
                    }

                    var (openCount, closeCount) = CountBracesOutsideQuotes(trimmed);
                    bool isOneLiner = openCount > 0 && closeCount > 0 && openCount == closeCount && trimmed.IndexOf('{') < trimmed.IndexOf('}');

                    // If there are closing braces on this line (and it's not a balanced one-liner), decrement depth for each.
                    if (!isOneLiner && closeCount > 0)
                    {
                        for (int i = 0; i < closeCount; i++)
                        {
                            depth--;
                            DebugLogger.Log("Global", fileName, LogLevel.Raw, $"[Parser] Depth Out: {depth} (Found '}}' in {fileName})");
                        }

                        if (trimmed == "}")
                        {
                            OnTokenFound("}", "", "", depth, fileName, false);
                            continue;
                        }
                    }

                    var parts = trimmed.Split(new[] { '=', ':', '>', '<' }, 2, StringSplitOptions.None);

                    if (parts.Length >= 1)
                    {
                        string key = parts[0].Trim();
                        string op = DetermineOperator(trimmed);
                        string value = parts.Length > 1 ? parts[1].Trim() : "";

                        if (isOneLiner)
                            DebugLogger.Log("Global", fileName, LogLevel.Raw, $"[Parser] One-Liner Detected: {key} {op} {value}");

                        OnTokenFound(key, op, value, depth, fileName, isOneLiner);
                    }

                    // If there are opening braces on this line (and it's not a balanced one-liner), increment depth for each.
                    if (!isOneLiner && openCount > 0)
                    {
                        for (int i = 0; i < openCount; i++)
                        {
                            depth++;
                            DebugLogger.Log("Global", fileName, LogLevel.Raw, $"[Parser] Depth In: {depth} (Found '{{' in {fileName})");
                        }
                    }
                }

                bool isLocalisation = filePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
                if (depth != 0 && !isLocalisation)
                {
                    string errorType = depth > 0 ? "MISSING '}'" : "EXTRA '}'";
                    string message = $"[STRUCTURAL ERROR] File '{fileName}' ended with Depth {depth}. Likely {errorType}. Data after this point in the workspace may be corrupted.";

                    Context.StructuralErrors.Add(message);
                    DebugLogger.AddStructuralError(message);
                    DebugLogger.Log("Global", fileName, LogLevel.Error, message);
                }
            });
        }

        private string DetermineOperator(string line)
        {
            if (line.Contains(">=")) return ">=";
            if (line.Contains("<=")) return "<=";
            if (line.Contains("=")) return "=";
            if (line.Contains(":")) return ":";
            if (line.Contains(">")) return ">";
            if (line.Contains("<")) return "<";
            return "";
        }

        protected abstract void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner);
    }

    public static class RawLineHelper
    {
        // Builds a raw line string from the parsed token parts, logs depth and content, and returns the built string.
        public static string BuildAndLog(string key, string op, string rawValue, int depth)
        {
            string lineContent;
            if (key == "}")
            {
                lineContent = "}";
            }
            else if (!string.IsNullOrEmpty(rawValue) && rawValue.Contains("{"))
            {
                lineContent = $"{key} {op} {rawValue}";
            }
            else if (string.IsNullOrEmpty(rawValue) || rawValue == "{")
            {
                lineContent = $"{key} {op} {{";
            }
            else
            {
                lineContent = $"{key} {op} {rawValue}";
            }

            DebugLogger.Log("Global", "", LogLevel.Raw, $"Saved Raw Line (Depth:{depth}): {lineContent}");
            return lineContent;
        }
    }
}