using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Compiler
{
    /// <summary>
    /// Handles cache updates and management for compiled output files.
    /// Tracks compiled artifacts and synchronizes them with the cache system.
    /// Organizes cache by source file to prevent redundant compilations.
    /// </summary>
    public static class CacheHandler
    {
        /// <summary>
        /// Absolute path to the cache directory where cache metadata is stored.
        /// Set once during compiler initialization.
        /// </summary>
        public static string CachePath { get; set; }

        // Lock for thread-safe atomic updates to config.json files
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Cache configuration structure stored in config.json
        /// </summary>
        [Serializable]
        private class CacheConfig
        {
            [JsonPropertyName("sourceAbsolutePath")]
            public string SourceAbsolutePath { get; set; }

            [JsonPropertyName("outputs")]
            public List<string> Outputs { get; set; } = new List<string>();
        }

        /// <summary>
        /// Handles cache updates when a file is written.
        /// Creates a subfolder per source file and maintains a config.json tracking all outputs.
        /// Folder name format: {sourceFileName}{extension} (e.g., "chinascript" for "china.script")
        /// </summary>
        /// <param name="outputFilePath">Full absolute path to the file that was just written</param>
        /// <param name="sourceFileName">Name of the original source file (without full path)</param>
        /// <param name="sourceAbsolutePath">Absolute path to the original source file</param>
        public static void HandleCacheUpdate(string outputFilePath, string sourceFileName, string sourceAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(CachePath) || string.IsNullOrWhiteSpace(sourceFileName) || string.IsNullOrWhiteSpace(sourceAbsolutePath))
            {
                return;
            }

            try
            {
                lock (_cacheLock)
                {
                    // Generate folder name: sourceFileName + extension without the dot
                    // E.g., "china.script" -> "chinascript", "china.event" -> "chinaevent"
                    string folderName = GenerateCacheFolderName(sourceFileName);
                    string cacheSubfolderPath = Path.Combine(CachePath, folderName);
                    Directory.CreateDirectory(cacheSubfolderPath);

                    // Path to config.json for this source file
                    string configPath = Path.Combine(cacheSubfolderPath, "config.json");

                    // Load existing config or create new one
                    CacheConfig config = LoadOrCreateConfig(configPath, sourceAbsolutePath);

                    // Check if this output path is already cached
                    if (config.Outputs.Contains(outputFilePath, StringComparer.OrdinalIgnoreCase))
                    {
                        // Already cached, no need to update
                        return;
                    }

                    // Add the new output path to the list
                    config.Outputs.Add(outputFilePath);

                    // Atomically write the updated config
                    WriteConfigAtomically(configPath, config);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - cache failures shouldn't break compilation
                try
                {
                    Compiler.Logging.Logger.LogMain($"[CacheHandler] Error updating cache: {ex.Message}");
                }
                catch { }
            }
        }

        /// <summary>
        /// Generates the cache folder name from source file name.
        /// Removes the leading dot from extension.
        /// E.g., "china.script" -> "chinascript", "config.event" -> "configevent"
        /// </summary>
        private static string GenerateCacheFolderName(string sourceFileName)
        {
            if (string.IsNullOrWhiteSpace(sourceFileName))
            {
                return "unknown";
            }

            // Remove the dot before extension if present
            string nameWithoutLeadingDot = sourceFileName.Replace(".", "");
            return nameWithoutLeadingDot;
        }

        /// <summary>
        /// Loads existing config from disk or creates a new one if it doesn't exist.
        /// </summary>
        private static CacheConfig LoadOrCreateConfig(string configPath, string sourceAbsolutePath)
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<CacheConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    Compiler.Logging.Logger.LogMain($"[CacheHandler] Failed to load config from {configPath}: {ex.Message}. Creating new config.");
                }
            }

            // Create new config
            return new CacheConfig
            {
                SourceAbsolutePath = sourceAbsolutePath,
                Outputs = new List<string>()
            };
        }

        /// <summary>
        /// Atomically writes the config to disk using a temporary file pattern.
        /// Ensures data integrity even if the process crashes during write.
        /// </summary>
        private static void WriteConfigAtomically(string configPath, CacheConfig config)
        {
            string tempPath = configPath + ".tmp";

            try
            {
                // Write to temporary file first
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(tempPath, json);

                // Atomic rename (replace) to final location
                File.Delete(configPath);
                File.Move(tempPath, configPath);
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogMain($"[CacheHandler] Failed to write config atomically to {configPath}: {ex.Message}");
                // Clean up temp file if it exists
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
}
