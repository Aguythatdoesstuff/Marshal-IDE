using System;
using System.IO;
using System.Text.Json;

namespace Compiler
{
    public class DdsCompiler : BaseCompiler
    {
        /// <summary>
        /// Compiles DDS and TGA image files.
        /// Supports multiple image types: ideas, equipment, flag, focus, event, fallback.
        /// </summary>
        public override void Compile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SourceAbsolutePath) || !File.Exists(SourceAbsolutePath))
                {
                    Compiler.Logging.Logger.LogComponent("DdsCompiler", "SourceAbsolutePath missing or file not found.");
                    return;
                }

                var sourceFileName = Path.GetFileName(SourceAbsolutePath);
                var nameOnly = Path.GetFileNameWithoutExtension(sourceFileName);

                // Read image metadata from cache to determine output type
                var imageMetadata = ReadImageMetadata(nameOnly);

                // Delegate all output handling to ImageTypeHandler - it owns the output path logic
                ImageTypeHandler.HandleImageOutput(imageMetadata, sourceFileName, nameOnly, 
                    SourceAbsolutePath, OutputPath);

                Compiler.Logging.Logger.LogComponent("DdsCompiler", $"Processed {sourceFileName}");
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogComponent("DdsCompiler", "Failed: " + ex.Message);
                Compiler.Logging.Logger.ReportUnhandledException(ex);
                IPC.Send("FatalError", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Reads image metadata from the cache.
        /// Looks for config.json at: CachePath\gfx\{imageName (no extension)}\config.json
        /// </summary>
        private static ImageMetadata ReadImageMetadata(string imageName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CacheHandler.CachePath) || string.IsNullOrWhiteSpace(imageName))
                {
                    return null;
                }

                // CachePath points to fileOutCache, but image metadata is in parent metadata folder
                // Go up one level, then into gfx/{imageName}/config.json
                var metadataRoot = Path.Combine(CacheHandler.CachePath, "..");
                var cacheConfigPath = Path.Combine(metadataRoot, "gfx", imageName, "config.json");

                // Normalize the path to clean up the ..
                cacheConfigPath = Path.GetFullPath(cacheConfigPath);

                if (!File.Exists(cacheConfigPath))
                {
                    Compiler.Logging.Logger.LogComponent("DdsCompiler", $"Image cache metadata not found at {cacheConfigPath}");
                    return null;
                }

                var json = File.ReadAllText(cacheConfigPath);
                var metadata = JsonSerializer.Deserialize<ImageMetadata>(json);

                return metadata;
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogComponent("DdsCompiler", $"Error reading image metadata: {ex.Message}");
                return null;
            }
        }
    }
}
