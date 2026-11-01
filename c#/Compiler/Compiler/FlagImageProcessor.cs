using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;

namespace Compiler
{
    /// <summary>
    /// Processes flag images by resizing them to standard, medium, and small sizes.
    /// Uses Magick.NET for image manipulation and outputs to gfx/flags directory structure.
    /// </summary>
    public static class FlagImageProcessor
    {
        // Flag image size specifications
        private const int StandardWidth = 82;
        private const int StandardHeight = 52;
        private const int MediumWidth = 41;
        private const int MediumHeight = 26;
        private const int SmallWidth = 10;
        private const int SmallHeight = 7;

        /// <summary>
        /// Processes a flag image and outputs three sizes to the gfx/flags directory structure.
        /// </summary>
        /// <param name="sourceImagePath">Full path to the source image file</param>
        /// <param name="sourceFileName">Name of the source file (e.g., "usa_flag.dds")</param>
        /// <param name="sourceAbsolutePath">Absolute path to the original source file</param>
        /// <param name="metadata">Image metadata containing country tag and ideology group</param>
        /// <param name="flagsOutputDir">Base directory for flags output (gfx/flags)</param>
        public static void ProcessFlagImage(string sourceImagePath, string sourceFileName, string sourceAbsolutePath, ImageMetadata metadata, string flagsOutputDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
                {
                    Compiler.Logging.Logger.LogWarning("FlagImageProcessor", $"Source image not found: {sourceImagePath}");
                    return;
                }

                if (metadata == null || string.IsNullOrWhiteSpace(metadata.CountryTag))
                {
                    Compiler.Logging.Logger.LogWarning("FlagImageProcessor", "Metadata missing or country tag empty");
                    return;
                }

                // Generate output filename based on country tag and ideology group
                string outputFileName = GenerateFlagFileName(metadata);
                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Processing flag image for: {outputFileName}");

                // Collect all output files
                var outputFiles = new List<string>();

                // Create necessary directories - EXPLICITLY set paths
                var standardDir = Path.Combine(flagsOutputDir);
                var mediumDir = Path.Combine(flagsOutputDir, "medium");
                var smallDir = Path.Combine(flagsOutputDir, "small");

                Directory.CreateDirectory(standardDir);
                Directory.CreateDirectory(mediumDir);
                Directory.CreateDirectory(smallDir);

                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Flag output directories: Standard={standardDir}, Medium={mediumDir}, Small={smallDir}");

                // Load source image
                using (var sourceImage = new MagickImage(sourceImagePath))
                {
                    // Output Standard size (82x52) to flagsOutputDir
                    OutputStandardSize(sourceImage, standardDir, outputFileName, outputFiles);

                    // Output Medium size (41x26) to medium subdirectory
                    OutputMediumSize(sourceImage, mediumDir, outputFileName, outputFiles);

                    // Output Small size (10x7) to small subdirectory
                    OutputSmallSize(sourceImage, smallDir, outputFileName, outputFiles);
                }

                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Successfully processed flag image: {outputFileName}");

                // Cache all outputs once for this source file
                foreach (var outputFile in outputFiles)
                {
                    CacheHandler.HandleCacheUpdate(outputFile, sourceFileName, sourceAbsolutePath);
                }
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("FlagImageProcessor", $"Failed to process flag image: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates the output filename based on country tag and ideology group.
        /// Format: countryTag or countryTag_ideologyGroup if ideology group exists.
        /// </summary>
        private static string GenerateFlagFileName(ImageMetadata metadata)
        {
            string baseName = metadata.CountryTag ?? "unknown";

            if (!string.IsNullOrWhiteSpace(metadata.IdeologyGroup))
            {
                baseName = $"{baseName}_{metadata.IdeologyGroup}";
                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Using compound flag name with ideology group: {baseName}");
                return baseName + ".tga";
            }

            Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Using simple flag name (no ideology group): {baseName}");
            return baseName + ".tga";
        }

        /// <summary>
        /// Outputs the standard sized flag image (82x52 pixels).
        /// </summary>
        private static void OutputStandardSize(MagickImage sourceImage, string flagsOutputDir, string fileName, List<string> outputFiles)
        {
            try
            {
                var standardImage = new MagickImage(sourceImage);
                standardImage.Resize(StandardWidth, StandardHeight);
                standardImage.Format = MagickFormat.Tga;

                var standardPath = Path.Combine(flagsOutputDir, fileName);
                standardImage.Write(standardPath);

                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Created standard flag ({StandardWidth}x{StandardHeight}): {standardPath}");
                outputFiles.Add(standardPath);
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("FlagImageProcessor", $"Failed to create standard size flag: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Outputs the medium sized flag image (41x26 pixels).
        /// </summary>
        private static void OutputMediumSize(MagickImage sourceImage, string mediumDir, string fileName, List<string> outputFiles)
        {
            try
            {
                var mediumImage = new MagickImage(sourceImage);
                mediumImage.Resize(MediumWidth, MediumHeight);
                mediumImage.Format = MagickFormat.Tga;

                var mediumPath = Path.Combine(mediumDir, fileName);
                mediumImage.Write(mediumPath);

                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Created medium flag ({MediumWidth}x{MediumHeight}): {mediumPath}");
                outputFiles.Add(mediumPath);
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("FlagImageProcessor", $"Failed to create medium size flag: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Outputs the small sized flag image (10x7 pixels).
        /// </summary>
        private static void OutputSmallSize(MagickImage sourceImage, string smallDir, string fileName, List<string> outputFiles)
        {
            try
            {
                var smallImage = new MagickImage(sourceImage);
                smallImage.Resize(SmallWidth, SmallHeight);
                smallImage.Format = MagickFormat.Tga;

                var smallPath = Path.Combine(smallDir, fileName);
                smallImage.Write(smallPath);

                Compiler.Logging.Logger.LogComponent("FlagImageProcessor", $"Created small flag ({SmallWidth}x{SmallHeight}): {smallPath}");
                outputFiles.Add(smallPath);
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogWarning("FlagImageProcessor", $"Failed to create small size flag: {ex.Message}");
                throw;
            }
        }
    }
}
