using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace importer
{
    public class GFXImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("interface");
        public override IEnumerable<string> FileExtensions => new[] { ".gfx" };

        public override bool RequiresLocalisation => false;

        public class GFX
        {
            public string Id { get; set; }
            public string textureFile1 { get; set; }
            public string textureFile2 { get; set; }
            public string sizeX { get; set; }
            public string sizeY { get; set; }
            public string color { get; set; }
            public string colortwo { get; set; }
            public Boolean IsHorizontal { get; set; }
            public string steps { get; set; }
            public string Type { get; set; }
            public string FileName { get; set; }
            public Boolean insideProgressbar { get; set; }
        }

        private readonly AsyncLocal<GFX> _currentGFX = new AsyncLocal<GFX>();
        public ConcurrentBag<GFX> Results { get; } = new ConcurrentBag<GFX>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanKey = key?.Trim() ?? "";

                // Root wrapper check
                if (depth == 0 && cleanKey == "spriteTypes")
                {
                    return;
                }

                // Detect object start (spriteType or progressbartype)
                if (depth == 1 && (cleanKey == "progressbartype" || cleanKey == "spriteType") && op == "=")
                {
                    _currentGFX.Value = new GFX
                    {
                        insideProgressbar = (cleanKey == "progressbartype"),
                        Type = cleanKey,
                        FileName = fileName
                    };
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $">>> STARTED NEW OBJECT: {cleanKey} in {Path.GetFileName(fileName)}");
                    return;
                }

                var GFXValue = _currentGFX.Value;
                if (GFXValue == null) return;

                // Clean the value for processing
                string cleanValue = rawValueTrimmed.Trim('\"', '\'', '{', '}').Trim();

                // 1. Name/ID
                if (depth == 2 && cleanKey == "name" && op == "=")
                {
                    GFXValue.Id = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED ID: {cleanValue}");
                    return;
                }

                // 2. Textures
                if (depth == 2 && cleanKey == "textureFile1" && op == "=")
                {
                    GFXValue.textureFile1 = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED textureFile1: {cleanValue}");
                    return;
                }
                if (depth == 2 && cleanKey == "textureFile2" && op == "=")
                {
                    GFXValue.textureFile2 = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED textureFile2: {cleanValue}");
                    return;
                }

                // 3. Size Logic (One-Liner Fix)
                if (depth == 2 && cleanKey == "size" && op == "=" && isOneLiner)
                {
                    var xMatch = Regex.Match(cleanValue, @"x\s*=\s*(\d+)");
                    var yMatch = Regex.Match(cleanValue, @"y\s*=\s*(\d+)");
                    if (xMatch.Success) GFXValue.sizeX = xMatch.Groups[1].Value;
                    if (yMatch.Success) GFXValue.sizeY = yMatch.Groups[1].Value;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED SIZE (One-Liner): x={GFXValue.sizeX} y={GFXValue.sizeY}");
                    return;
                }

                // 4. Size Logic (Multi-Liner x/y)
                if (depth == 2 && cleanKey == "x" && op == "=")
                {
                    GFXValue.sizeX = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED sizeX: {cleanValue}");
                    return;
                }
                if (depth == 2 && cleanKey == "y" && op == "=")
                {
                    GFXValue.sizeY = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED sizeY: {cleanValue}");
                    return;
                }

                // 5. Color Logic (One-Liners and Multi-Liners)
                if (depth == 2 && cleanKey == "color" && op == "=")
                {
                    var parts = cleanValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    GFXValue.color = string.Join(" ", parts);
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED color: {GFXValue.color}");
                    return;
                }
                if (depth == 2 && cleanKey == "colortwo" && op == "=")
                {
                    var parts = cleanValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    GFXValue.colortwo = string.Join(" ", parts);
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED colortwo: {GFXValue.colortwo}");
                    return;
                }

                // 6. Miscellaneous
                if (depth == 2 && cleanKey == "horizontal" && op == "=")
                {
                    GFXValue.IsHorizontal = cleanValue.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED horizontal: {GFXValue.IsHorizontal}");
                    return;
                }
                if (depth == 2 && cleanKey == "steps" && op == "=")
                {
                    GFXValue.steps = cleanValue;
                    DebugLogger.Log("GFX", fileName, LogLevel.Info, $"SAVED steps: {cleanValue}");
                    return;
                }

                // 7. End of Object
                if (depth == 1 && cleanKey == "}")
                {
                    if (!string.IsNullOrEmpty(GFXValue.Id))
                    {
                        Results.Add(GFXValue);
                        DebugLogger.Log("GFX", fileName, LogLevel.Info, $"<<< FINISHED & STORED: {GFXValue.Id}");
                    }
                    else
                    {
                        DebugLogger.Log("GFX", fileName, LogLevel.Warning, $"!!! DISCARDED: Object ended but had no ID.");
                    }
                    _currentGFX.Value = null;
                    return;
                }
            }
        }
    }
}