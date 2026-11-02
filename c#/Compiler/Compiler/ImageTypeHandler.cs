using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using ImageMagick;

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
        /// Optionally resizes image if maxWidth and maxHeight are provided.
        /// </summary>
        private static void HandleDefaultOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath, int? maxWidth = null, int? maxHeight = null)
        {
            try
            {
                var outputFiles = new List<string>();

                // Create output directories
                var imageOutDir = Path.Combine(outputPath ?? string.Empty, "gfx", "interface", "handledByMarshalIDE");
                var gfxOutDir = Path.Combine(outputPath ?? string.Empty, "interface", "handledByMarshalIDE");

                Directory.CreateDirectory(imageOutDir);
                Directory.CreateDirectory(gfxOutDir);

                // Resize image if max dimensions provided
                string imageToCopy = sourceAbsolutePath;
                string tempResizedPath = null;
                if (maxWidth.HasValue && maxHeight.HasValue)
                {
                    try
                    {
                        tempResizedPath = ResizeImageFile(sourceAbsolutePath, maxWidth.Value, maxHeight.Value);
                        if (tempResizedPath != sourceAbsolutePath)
                        {
                            imageToCopy = tempResizedPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to resize image {sourceFileName}, using original: {ex.Message}");
                        imageToCopy = sourceAbsolutePath;
                    }
                }

                // Copy image file
                var destImagePath = Path.Combine(imageOutDir, sourceFileName);
                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying image to: {destImagePath}");
                File.Copy(imageToCopy, destImagePath, true);
                outputFiles.Add(destImagePath);

                // Clean up temp file if created
                if (tempResizedPath != null && tempResizedPath != sourceAbsolutePath && File.Exists(tempResizedPath))
                {
                    try { File.Delete(tempResizedPath); } catch { }
                }

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

                // Resize image to max 65x67 before copying
                string imageToCopy = sourceAbsolutePath;
                string tempResizedPath = null;
                try
                {
                    tempResizedPath = ResizeImageFile(sourceAbsolutePath, 65, 67);
                    if (tempResizedPath != sourceAbsolutePath)
                    {
                        imageToCopy = tempResizedPath;
                    }
                }
                catch (Exception ex)
                {
                    Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to resize IDEAS image {sourceFileName}, using original: {ex.Message}");
                    imageToCopy = sourceAbsolutePath;
                }

                // Copy image file
                var destImagePath = Path.Combine(imageOutDir, sourceFileName);
                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying IDEAS image to: {destImagePath}");
                File.Copy(imageToCopy, destImagePath, true);
                outputFiles.Add(destImagePath);

                // Clean up temp file if created
                if (tempResizedPath != null && tempResizedPath != sourceAbsolutePath && File.Exists(tempResizedPath))
                {
                    try { File.Delete(tempResizedPath); } catch { }
                }

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

                // Resize image to max 146x54 before copying
                string imageToCopy = sourceAbsolutePath;
                string tempResizedPath = null;
                try
                {
                    tempResizedPath = ResizeImageFile(sourceAbsolutePath, 146, 54);
                    if (tempResizedPath != sourceAbsolutePath)
                    {
                        imageToCopy = tempResizedPath;
                    }
                }
                catch (Exception ex)
                {
                    Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to resize EQUIPMENT image {sourceFileName}, using original: {ex.Message}");
                    imageToCopy = sourceAbsolutePath;
                }

                // Rename the image to use equipmentId instead of the original name
                var extension = Path.GetExtension(sourceFileName);
                var equipmentImageName = metadata.EquipmentId + extension;
                var equipmentImagePath = Path.Combine(imageOutDir, equipmentImageName);

                Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Copying EQUIPMENT image from '{sourceFileName}' to '{equipmentImageName}'");
                File.Copy(imageToCopy, equipmentImagePath, true);

                // Clean up temp file if created
                if (tempResizedPath != null && tempResizedPath != sourceAbsolutePath && File.Exists(tempResizedPath))
                {
                    try { File.Delete(tempResizedPath); } catch { }
                }

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
        /// Resizes to max 84x84 while preserving aspect ratio.
        /// </summary>
        private static void HandleFocusOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling FOCUS output: resizing and delegating to default handler for: {sourceFileName}");
            HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath, maxWidth: 84, maxHeight: 84);
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
        /// Resizes to max 400x116 while preserving aspect ratio.
        /// </summary>
        private static void HandleEventOutput(string sourceFileName, string nameOnly, 
            string sourceAbsolutePath, string outputPath)
        {
            Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Handling EVENT output: resizing and delegating to default handler for: {sourceFileName}");
            HandleDefaultOutput(sourceFileName, nameOnly, sourceAbsolutePath, outputPath, maxWidth: 400, maxHeight: 116);
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

        /// <summary>
        /// Calculates resized dimensions that fit within maxWidth and maxHeight while preserving aspect ratio.
        /// Returns the original dimensions if they already fit within bounds.
        /// </summary>
        private static (int width, int height) ResizeImageToFitWithin(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            // If already fits, return original dimensions
            if (originalWidth <= maxWidth && originalHeight <= maxHeight)
            {
                return (originalWidth, originalHeight);
            }

            // Calculate aspect ratio
            double aspectRatio = (double)originalWidth / originalHeight;

            // Determine which dimension to constrain based on aspect ratio
            int newWidth = maxWidth;
            int newHeight = (int)(maxWidth / aspectRatio);

            // If calculated height exceeds maxHeight, constrain by height instead
            if (newHeight > maxHeight)
            {
                newHeight = maxHeight;
                newWidth = (int)(maxHeight * aspectRatio);
            }

            return (newWidth, newHeight);
        }

        /// <summary>
        /// Resizes an image file to fit within max dimensions while preserving aspect ratio.
        /// Creates a temporary resized copy and returns the path to the resized image.
        /// </summary>
        private static string ResizeImageFile(string sourcePath, int maxWidth, int maxHeight)
        {
            try
            {
                using (var originalImage = new MagickImage(sourcePath))
                {
                    // Get original dimensions
                    int originalWidth = (int)originalImage.Width;
                    int originalHeight = (int)originalImage.Height;

                    // Calculate new dimensions preserving aspect ratio
                    var (newWidth, newHeight) = ResizeImageToFitWithin(originalWidth, originalHeight, maxWidth, maxHeight);

                    // If no resize needed, return original path
                    if (newWidth == originalWidth && newHeight == originalHeight)
                    {
                        Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Image {Path.GetFileName(sourcePath)} already fits within {maxWidth}x{maxHeight} bounds");
                        return sourcePath;
                    }

                    // Create resized image with aspect ratio preserved
                    var resizedImage = new MagickImage(originalImage);
                    var geometry = new MagickGeometry((uint)newWidth, (uint)newHeight) { IgnoreAspectRatio = false };
                    resizedImage.Resize(geometry);

                    // Create temporary file for resized image (same extension as original)
                    string tempDir = Path.Combine(Path.GetTempPath(), "MarshalIDEImageResize");
                    Directory.CreateDirectory(tempDir);
                    string tempPath = Path.Combine(tempDir, Guid.NewGuid().ToString() + Path.GetExtension(sourcePath));

                    // Preserve original format
                    resizedImage.Write(tempPath);
                    resizedImage.Dispose();

                    Compiler.Logging.Logger.LogComponent("ImageTypeHandler", $"Resized image {Path.GetFileName(sourcePath)} from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}");
                    return tempPath;
                }
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("ImageTypeHandler", $"Failed to resize image {Path.GetFileName(sourcePath)}: {ex.Message}");
                throw;
            }
        }
    }
}
