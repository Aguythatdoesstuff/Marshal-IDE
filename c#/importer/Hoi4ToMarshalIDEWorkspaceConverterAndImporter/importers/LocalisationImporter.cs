using System;
using System.Collections.Generic;
using System.Text;

namespace importer
{
    public class LocalisationImporter : BaseImporter
    {
        public override string FolderSubPath => "localisation";
        // Localisation cares about YAML files with either .yml or .yaml extensions
        public override IEnumerable<string> FileExtensions => new[] { ".yml", ".yaml" };
        // Localisation never waits for itself (that would be weird)
        public override bool RequiresLocalisation => false;

        // We keep track of the currently active language per file because files are processed
        // concurrently and the importer instance may receive tokens for multiple files at once.
        private System.Collections.Concurrent.ConcurrentDictionary<string, string> _currentLanguagePerFile =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            // Normalize operator and key
            op = op?.Trim();
            key = key?.Trim();

            // Detect language header lines like: l_english:
            if ((op == ":") && key.StartsWith("l_", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(value))
            {
                // Guard: Ensure "l_" isn't the ONLY thing in the string
                var lang = key.Length > 2 ? key.Substring(2) : "unknown";
                var dict = Context.Languages.GetOrAdd(lang, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                _currentLanguagePerFile[fileName] = lang;
                DebugLogger.Log("LocalisationImporter", fileName, LogLevel.Info, $"Detected language '{lang}' in {fileName}.");
                return;
            }

            if (!_currentLanguagePerFile.TryGetValue(fileName, out var currentLang))
            {
                // Silently skip lines before the language header is found to avoid log spam
                return;
            }

            if (op != ":") return;

            // Use null-coalescing to guarantee we never work with a null string
            var cleaned = value?.Trim() ?? string.Empty;

            if (cleaned.Length > 0 && char.IsDigit(cleaned[0]))
            {
                int i = 0;
                while (i < cleaned.Length && char.IsDigit(cleaned[i])) i++;
                cleaned = cleaned.Substring(i).Trim();
            }

            if (cleaned.Length >= 2)
            {
                bool startsWithQuote = cleaned.StartsWith("\"") || cleaned.StartsWith("'");
                bool endsWithQuote = cleaned.EndsWith("\"") || cleaned.EndsWith("'");

                if (startsWithQuote && endsWithQuote)
                {
                    // Standard properly formatted string
                    cleaned = cleaned.Substring(1, cleaned.Length - 2);
                }
                else if (startsWithQuote)
                {
                    // Missing closing quote (Human error!)
                    cleaned = cleaned.Substring(1);
                    DebugLogger.Log("LocalisationImporter", fileName, LogLevel.Warning, $"Missing closing quote for key '{key}' in {fileName}");
                }
            }
            else if (cleaned == "\"" || cleaned == "'")
            {
                // Extreme edge case: The string is literally just one quote and nothing else
                cleaned = string.Empty;
            }

            // Unescape common escaped quotes
            cleaned = cleaned.Replace("\\\"", "\"");

            var langDict = Context.Languages.GetOrAdd(currentLang, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            if (!langDict.TryAdd(key, cleaned))
            {
                var warning = $"Duplicate localisation id '{key}' in language '{currentLang}' (file: {fileName}). Ignoring duplicate.";
                Context.LocalisationWarnings.Add(warning);
                DebugLogger.Log("LocalisationImporter", fileName, LogLevel.Warning, warning);
            }
            else
            {
                // NOTE: If you want to speed up the import of vanilla files, comment this next line out!
                DebugLogger.Log("LocalisationImporter", fileName, LogLevel.Info, $"Saved: {currentLang}.{key} = '{cleaned}' (from {fileName})");
            }
        }
    }
}
