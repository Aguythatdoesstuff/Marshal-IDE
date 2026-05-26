using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace importer
{
    // Simple importer that copies .dds files from input 'gfx' folder into output 'mod/GFX' preserving subfolders.
    // This importer does not depend on any other importer.
    public class DDSImporter
    {
        // Simple standalone copier. Does not inherit from BaseImporter to avoid the base parser reading binary files.
        private readonly ConcurrentDictionary<string, bool> _copiedFiles = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public async Task RunAsync(string inputRootDirectory, string outputRootDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(inputRootDirectory)) inputRootDirectory = Directory.GetCurrentDirectory();

            string expected = Path.Combine(inputRootDirectory, "gfx");
            string finalPath = expected;
            if (!Directory.Exists(finalPath))
            {
                try
                {
                    finalPath = Directory.EnumerateDirectories(inputRootDirectory, "gfx", SearchOption.AllDirectories).FirstOrDefault() ?? finalPath;
                    if (Directory.Exists(finalPath))
                        DebugLogger.Log("DDSImporter", finalPath, LogLevel.Info, $"Found gfx folder at: {finalPath}");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("DDSImporter", finalPath, LogLevel.Warning, $"Searching for gfx folder failed: {ex.Message}");
                }
            }

            if (!Directory.Exists(finalPath))
            {
                DebugLogger.Log("DDSImporter", inputRootDirectory, LogLevel.Info, $"No gfx folder found under {inputRootDirectory}. Nothing to copy.");
                return;
            }

            var files = Directory.EnumerateFiles(finalPath, "*.dds", SearchOption.AllDirectories).ToList();
            DebugLogger.Log("DDSImporter", finalPath, LogLevel.Info, $"Found {files.Count} .dds files in {finalPath}");

            var outRoot = string.IsNullOrWhiteSpace(outputRootDirectory) ? inputRootDirectory : outputRootDirectory;
            var outputBase = Path.Combine(outRoot, "mod", "GFX");
            int copied = 0;

            var tasks = files.Select(f => Task.Run(() =>
            {
                try
                {
                    var src = Path.GetFullPath(f);
                    var rel = Path.GetRelativePath(finalPath, src);
                    var dest = Path.Combine(outputBase, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? outputBase);
                    File.Copy(src, dest, true);
                    DebugLogger.Log("DDSImporter", dest, LogLevel.Info, $"Copied: {src} -> {dest}");
                    System.Threading.Interlocked.Increment(ref copied);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("DDSImporter", finalPath, LogLevel.Error, ex.Message);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            DebugLogger.Log("DDSImporter", finalPath, LogLevel.Info, $"Completed. Copied {copied} files.");
        }
    }
}
