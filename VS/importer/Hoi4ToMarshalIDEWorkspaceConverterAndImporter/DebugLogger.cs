using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace importer
{
    public enum LogLevel { Info, Warning, Error, Fatal, Raw }

    public class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
    }

    public static class DebugLogger
    {
        private static string _rootDir = null;

        // We still need to hold entries for ONE file at a time to generate the organized MD structure.
        // To save RAM, we clear the bag for a file once it is written to disk.
        private static ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<LogEntry>>> _logs
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<LogEntry>>>();

        private static ConcurrentBag<string> _structuralErrors = new ConcurrentBag<string>();
        private static ConcurrentBag<string> _criticalErrors = new ConcurrentBag<string>();

        public static void Initialize(string debugOutputPath, string inputPath)
        {
            // Logic: Use debug path if provided, otherwise fallback to input path/Debbug
            if (!string.IsNullOrWhiteSpace(debugOutputPath))
                _rootDir = Path.Combine(debugOutputPath, "Debbug");
            else if (!string.IsNullOrWhiteSpace(inputPath))
                _rootDir = Path.Combine(inputPath, "Debbug");
            else
                _rootDir = Path.Combine(Directory.GetCurrentDirectory(), "Debbug");

            Directory.CreateDirectory(_rootDir);
            _logs.Clear();
            _structuralErrors = new ConcurrentBag<string>();
            _criticalErrors = new ConcurrentBag<string>();
        }

        public static void Log(string importerName, string filePath, LogLevel level, string message)
        {
            try
            {
                if (importerName == "CRASH_HANDLER")
                {
                    // Special case: we want to log crash handler messages to a global file, not per-importer.
                    importerName = "Global";
                    filePath = "global";
                }
                if (string.IsNullOrEmpty(importerName)) importerName = "Global";
                if (string.IsNullOrEmpty(filePath)) filePath = "global";
                var fileDict = _logs.GetOrAdd(importerName, _ => new ConcurrentDictionary<string, ConcurrentBag<LogEntry>>());
                var bag = fileDict.GetOrAdd(filePath, _ => new ConcurrentBag<LogEntry>());
                bag.Add(new LogEntry { Time = DateTime.UtcNow, Level = level, Message = message });
            }
            catch { /* swallow logging errors */ }
        }

        public static void FlushFile(string importerName, string filePath)
        {
            if (!_logs.TryGetValue(importerName, out var fileDict)) return;
            if (!fileDict.TryRemove(filePath, out var bag)) return;

            var impFolder = Path.Combine(_rootDir, importerName);
            Directory.CreateDirectory(impFolder);

            var safeName = MakeSafeFileName(Path.GetFileName(filePath));
            if (string.IsNullOrEmpty(safeName)) safeName = "global";
            var outFile = Path.Combine(impFolder, safeName + ".md");

            var entries = bag.ToList();
            var ordered = entries.OrderBy(e => LevelPriority(e.Level)).ThenBy(e => e.Time).ToList();

            using (var sw = new StreamWriter(outFile, false, Encoding.UTF8))
            {
                sw.WriteLine($"# Logs for {importerName} - {safeName}");
                sw.WriteLine();

                // Summary counts
                var counts = ordered.GroupBy(e => e.Level).ToDictionary(g => g.Key, g => g.Count());
                sw.WriteLine("## Summary");
                foreach (LogLevel lv in Enum.GetValues(typeof(LogLevel)))
                {
                    counts.TryGetValue(lv, out int c);
                    sw.WriteLine($"- {lv}: {c}");
                }
                sw.WriteLine();

                // Fatal / Error
                sw.WriteLine("## Errors and Fatal");
                foreach (var e in ordered.Where(x => x.Level == LogLevel.Fatal || x.Level == LogLevel.Error))
                    sw.WriteLine($"- [{e.Time:O}] **{e.Level}**: {e.Message}");
                sw.WriteLine();

                sw.WriteLine("## Warnings");
                foreach (var e in ordered.Where(x => x.Level == LogLevel.Warning))
                    sw.WriteLine($"- [{e.Time:O}] {e.Message}");
                sw.WriteLine();

                sw.WriteLine("## Info");
                foreach (var e in ordered.Where(x => x.Level == LogLevel.Info))
                    sw.WriteLine($"- [{e.Time:O}] {e.Message}");
                sw.WriteLine();

                sw.WriteLine("## Raw / Untouched");
                foreach (var e in ordered.Where(x => x.Level == LogLevel.Raw))
                    sw.WriteLine($"- [{e.Time:O}] {e.Message}");
            }
        }

        public static void AddStructuralError(string message) => _structuralErrors.Add(message);
        public static void AddCriticalError(string message) => _criticalErrors.Add(message);

        public static void WriteOut(IEnumerable<BaseImporter> importers, List<(string Name, TimeSpan Duration, bool Success)> compilerResults, double totalSeconds, int totalImportedFiles)
        {
            // Final safety flush for anything remaining in RAM
            foreach (var impPair in _logs.ToList())
                foreach (var filePair in impPair.Value.Keys.ToList())
                    FlushFile(impPair.Key, filePair);

            var topFile = Path.Combine(_rootDir, "Debbug.md");
            using (var sw = new StreamWriter(topFile, false, Encoding.UTF8))
            {
                sw.WriteLine("# Debug Report");
                sw.WriteLine();
                sw.WriteLine("## Importer Metrics");
                sw.WriteLine();
                foreach (var imp in importers)
                    sw.WriteLine($"- {imp.GetType().Name}: Files={imp.ProcessedFileCount}, Time={imp.ImportDuration.TotalSeconds:F2}s, MemBefore={imp.MemoryBeforeBytes}, MemAfter={imp.MemoryAfterBytes}");

                sw.WriteLine();
                sw.WriteLine("## Compiler Metrics");
                foreach (var cr in compilerResults)
                    sw.WriteLine($"- {cr.Name}: Time={cr.Duration.TotalSeconds:F2}s, Success={(cr.Success ? "Yes" : "No")}");

                sw.WriteLine();
                sw.WriteLine("## Performance");
                sw.WriteLine($"- Total Time: {totalSeconds:F2} seconds");
                sw.WriteLine($"- Total files processed by importers: {totalImportedFiles}");
                sw.WriteLine();

                sw.WriteLine("## Structural Errors");
                foreach (var e in _structuralErrors) sw.WriteLine($"- {e}");
                sw.WriteLine();

                sw.WriteLine("## Critical / Fatal Errors");
                foreach (var e in _criticalErrors) sw.WriteLine($"- {e}");
                sw.WriteLine();

                sw.WriteLine("## Per-importer logs");
                // We read the directory structure to recreate the links since RAM was cleared
                if (Directory.Exists(_rootDir))
                {
                    foreach (var dir in Directory.GetDirectories(_rootDir).OrderBy(d => d))
                    {
                        var dirName = Path.GetFileName(dir);
                        sw.WriteLine($"- {dirName}:");
                        foreach (var file in Directory.GetFiles(dir, "*.md").OrderBy(f => f))
                        {
                            var fileName = Path.GetFileName(file);
                            sw.WriteLine($"  - [{fileName}]({dirName}/{fileName})");
                        }
                    }
                }
            }
        }

        private static int LevelPriority(LogLevel l)
        {
            return l switch
            {
                LogLevel.Fatal => 0,
                LogLevel.Error => 1,
                LogLevel.Warning => 2,
                LogLevel.Info => 3,
                LogLevel.Raw => 4,
                _ => 5
            };
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        public static async Task WriteCrashReportAsync()
        {
            // Ensure the root directory exists
            if (string.IsNullOrEmpty(_rootDir)) _rootDir = Path.Combine(Directory.GetCurrentDirectory(), "Debbug");
            Directory.CreateDirectory(_rootDir);

            var crashFile = Path.Combine(_rootDir, $"CRASH_REPORT_{DateTime.Now:yyyyMMdd_HHmmss}.md");

            // We pull the logs from the "Global" bucket where CRASH_HANDLER logs
            if (_logs.TryGetValue("Global", out var fileDict) && fileDict.TryGetValue("global", out var bag))
            {
                var entries = bag.OrderBy(e => e.Time).ToList();
                using (var sw = new StreamWriter(crashFile, false, Encoding.UTF8))
                {
                    await sw.WriteLineAsync("# EMERGENCY CRASH REPORT");
                    foreach (var e in entries)
                    {
                        await sw.WriteLineAsync(e.Message);
                    }
                }
            }
        }
    }
}