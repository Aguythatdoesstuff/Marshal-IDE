using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace Compiler
{
    /// <summary>
    /// Represents the metadata of an image stored in the image cache.
    /// Deserialized from config.json files in cachePath\gfx\{imageName}\.
    /// Only includes fields used by the compiler; other fields are ignored by JsonSerializer.
    /// </summary>
    [Serializable]
    public class ImageMetadata
    {
        [JsonPropertyName("countryTag")]
        public string CountryTag { get; set; } = string.Empty;

        [JsonPropertyName("imageType")]
        public string ImageType { get; set; } = string.Empty;

        [JsonPropertyName("equipmentId")]
        public string EquipmentId { get; set; } = string.Empty;

        [JsonPropertyName("ideologyGroup")]
        public string IdeologyGroup { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles output generation for different image types.
    /// Provides type-specific methods that can be easily extended or modified.
    /// </summary>
    public static class ImageTypeHandler
    {
        /// <summary>
        /// Generates output files based on image type.
        /// Each type handler owns its own output path logic.
        /// </summary>
        public static void HandleImageOutput(ImageMetadata metadata, string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Starting HandleImageOutput for image: {sourceFileName}");

            // If metadata is missing, use default output
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.ImageType))
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"No metadata or image type found for {sourceFileName}, using default output handler");
                HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                return;
            }

            var imageType = metadata.ImageType.ToLowerInvariant();
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Processing image type '{imageType}' for {sourceFileName} (country: {metadata.CountryTag})");

            switch (imageType)
            {
                case "ideas":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling IDEAS image: {sourceFileName}");
                    HandleIdeasOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                case "equipment":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling EQUIPMENT image: {sourceFileName} (equipmentId: {metadata.EquipmentId})");
                    HandleEquipmentOutput(metadata, sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                case "focus":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FOCUS image: {sourceFileName}");
                    HandleFocusOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                case "flag":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FLAG image: {sourceFileName}");
                    HandleFlagOutput(metadata, sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                case "event":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling EVENT image: {sourceFileName}");
                    HandleEventOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                case "fallback":
                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FALLBACK image: {sourceFileName}");
                    HandleFallbackOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;

                default:
                    Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Unknown image type '{imageType}' for {sourceFileName}, using default output handler");
                    HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
                    break;
            }

            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Completed HandleImageOutput for image: {sourceFileName}");
        }

        /// <summary>
        /// Default output: copies image to gfx/interface/handledByMarshalIDE and creates a .gfx definition file with GFX_ prefix.
        /// Used for: focus, event, fallback, and unknown types.
        /// </summary>
        private static void HandleDefaultOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            try
            {
                var outputFiles = new List<string>();

                // Create output directories
                var imageOutDir = Path.Combine(outputPath ?? string.Empty, "gfx", "interface", "handledByMarshalIDE");
                var gfxOutDir = Path.Combine(outputPath ?? string.Empty, "interface", "handledByMarshalIDE");

                Directory.CreateDirectory(imageOutDir);
                Directory.CreateDirectory(gfxOutDir);

                // Copy image file
                var destImagePath = Path.Combine(imageOutDir, sourceFileName);
                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying image to: {destImagePath}");
                File.Copy(sourceAbsolutePath, destImagePath, true);
                outputFiles.Add(destImagePath);

                // Create GFX definition file
                var gfxFileName = nameOnly + ".gfx";
                var gfxFilePath = Path.Combine(gfxOutDir, gfxFileName);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Creating default GFX definition file: {gfxFileName}");

                using (var sw = new StreamWriter(new FileStream(gfxFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), 
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    sw.WriteLine("spriteTypes = {");
                    sw.WriteLine("    spriteType = {");
                    sw.WriteLine($"        name = \"GFX_{nameOnly}\"");
                    sw.WriteLine($"        texturefile = \"gfx/interface/handledByMarshalIDE/{sourceFileName}\"");
                    sw.WriteLine("    }");
                    sw.WriteLine("}");
                }

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Successfully created GFX definition file: {gfxFilePath}");
                outputFiles.Add(gfxFilePath);

                // Cache all outputs at once for this source file - use sourceFileName to create the cache folder
                foreach (var outputFile in outputFiles)
                {
                    CacheHandler.HandleCacheUpdate(outputFile, sourceFileName, sourceAbsolutePath);
                }
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to create default output for {sourceFileName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ideas output: copies image and creates a .gfx definition file with GFX_idea_ prefix.
        /// </summary>
        private static void HandleIdeasOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            try
            {
                var outputFiles = new List<string>();

                // Create output directories
                var imageOutDir = Path.Combine(outputPath ?? string.Empty, "gfx", "interface", "handledByMarshalIDE");
                var gfxOutDir = Path.Combine(outputPath ?? string.Empty, "interface", "handledByMarshalIDE");

                Directory.CreateDirectory(imageOutDir);
                Directory.CreateDirectory(gfxOutDir);

                // Copy image file
                var destImagePath = Path.Combine(imageOutDir, sourceFileName);
                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying IDEAS image to: {destImagePath}");
                File.Copy(sourceAbsolutePath, destImagePath, true);
                outputFiles.Add(destImagePath);

                // Create GFX definition file with GFX_idea_ prefix
                var gfxFileName = nameOnly + ".gfx";
                var gfxFilePath = Path.Combine(gfxOutDir, gfxFileName);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Creating IDEAS GFX definition file with GFX_idea_ prefix: {gfxFileName}");

                using (var sw = new StreamWriter(new FileStream(gfxFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), 
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    sw.WriteLine("spriteTypes = {");
                    sw.WriteLine("    spriteType = {");
                    sw.WriteLine($"        name = \"GFX_idea_{nameOnly}\"");
                    sw.WriteLine($"        texturefile = \"gfx/interface/handledByMarshalIDE/{sourceFileName}\"");
                    sw.WriteLine("    }");
                    sw.WriteLine("}");
                }

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Successfully created IDEAS GFX definition file: {gfxFilePath}");
                outputFiles.Add(gfxFilePath);

                // Cache all outputs at once for this source file - use sourceFileName to create the cache folder
                foreach (var outputFile in outputFiles)
                {
                    CacheHandler.HandleCacheUpdate(outputFile, sourceFileName, sourceAbsolutePath);
                }
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to create IDEAS output for {sourceFileName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Equipment output: copies the image to gfx/interface/handledByMarshalIDE and renames it to use the equipmentId.
        /// No .gfx definition file is created.
        /// </summary>
        private static void HandleEquipmentOutput(ImageMetadata metadata, string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(metadata.EquipmentId))
                {
                    Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Equipment image missing equipmentId in metadata for {sourceFileName}, skipping output.");
                    return;
                }

                // Create output directory
                var imageOutDir = Path.Combine(outputPath ?? string.Empty, "gfx", "interface", "handledByMarshalIDE");
                Directory.CreateDirectory(imageOutDir);

                // Rename the image to use equipmentId instead of the original name
                var extension = Path.GetExtension(sourceFileName);
                var equipmentImageName = metadata.EquipmentId + extension;
                var equipmentImagePath = Path.Combine(imageOutDir, equipmentImageName);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying EQUIPMENT image from '{sourceFileName}' to '{equipmentImageName}'");
                File.Copy(sourceAbsolutePath, equipmentImagePath, true);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Successfully created equipment image file: {equipmentImagePath}");
                CacheHandler.HandleCacheUpdate(equipmentImagePath, sourceFileName, sourceAbsolutePath);
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to create equipment output for {sourceFileName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Focus output: copies image and creates a .gfx definition file with GFX_ prefix.
        /// </summary>
        private static void HandleFocusOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FOCUS output: delegating to default handler for: {sourceFileName}");
            HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
        }

        /// <summary>
        /// Flag output: resizes flag images to standard, medium, and small sizes.
        /// Outputs ONLY to gfx/flags (no generic interface output).
        /// </summary>
        private static void HandleFlagOutput(ImageMetadata metadata, string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            try
            {
                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FLAG output for: {sourceFileName}");

                // Construct the flags output directory
                var flagsOutputDir = Path.Combine(outputPath ?? string.Empty, "gfx", "flags");

                // Use FlagImageProcessor to handle resizing and output
                FlagImageProcessor.ProcessFlagImage(sourceAbsolutePath, sourceFileName, sourceAbsolutePath, metadata, flagsOutputDir);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Successfully processed flag image via FlagImageProcessor");
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to handle flag output for {sourceFileName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Event output: copies image and creates a .gfx definition file with GFX_ prefix.
        /// </summary>
        private static void HandleEventOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling EVENT output: delegating to default handler for: {sourceFileName}");
            HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
        }

        /// <summary>
        /// Fallback output: copies image and creates a .gfx definition file with GFX_ prefix.
        /// </summary>
        private static void HandleFallbackOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FALLBACK output: delegating to default handler for: {sourceFileName}");
            HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath);
        }
    }
}
