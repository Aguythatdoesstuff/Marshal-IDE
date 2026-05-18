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

// todo: save what order all ellements are

namespace importer
{
    public class InterfaceImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("interface");
        public override IEnumerable<string> FileExtensions => new[] { ".gui" };
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class Background
        {
            public string Id { get; set; }
            public string quadTextureSprite { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class instantTextBoxType
        {
            public string Id { get; set; }
            public string textLoc { get; set; }
            public string font { get; set; }
            public string positionX { get; set; }
            public string positionY { get; set; }
            public string maxWidth { get; set; }
            public string maxHeight { get; set; }
            public string pdx_tooltip { get; set; }
            public string orderNumber { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class buttonType
        {
            public string Id { get; set; }
            public string font { get; set; }
            public string textLoc { get; set; }
            public string spriteType { get; set; }
            public string positionX { get; set; }
            public string positionY { get; set; }
            public string pdx_tooltip { get; set; }
            public string scale { get; set; }
            public string orderNumber { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class iconType
        {
            public string Id { get; set; }
            public string spriteType { get; set; }
            public string positionX { get; set; }
            public string positionY { get; set; }
            public string sizeX { get; set; }
            public string sizeY { get; set; }
            public string pdx_tooltip { get; set; }
            public string scale { get; set; }
            public string orderNumber { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class Interface
        {
            public string Id { get; set; }
            public bool moveable { get; set; } = false;
            public string positionX { get; set; }
            public string positionY { get; set; }
            public string sizeX { get; set; }
            public string sizeY { get; set; }
            public string Type { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
            public List<Background> Background { get; set; } = new List<Background>();
            public List<instantTextBoxType> instantTextBoxType { get; set; } = new List<instantTextBoxType>();
            public List<buttonType> buttonType { get; set; } = new List<buttonType>();
            public List<iconType> iconType { get; set; } = new List<iconType>();
            public int OrderCounter { get; set; } = 0;
        }

        private readonly AsyncLocal<Interface> _currentInterface = new AsyncLocal<Interface>();
        private readonly AsyncLocal<Background> _currentBackground = new AsyncLocal<Background>();
        private readonly AsyncLocal<instantTextBoxType> _currentinstantTextBoxType = new AsyncLocal<instantTextBoxType>();
        private readonly AsyncLocal<buttonType> _currentbuttonType = new AsyncLocal<buttonType>();
        private readonly AsyncLocal<iconType> _currenticonType = new AsyncLocal<iconType>();
        private readonly AsyncLocal<string> _insideGuiElement = new AsyncLocal<string> { Value = "none" };

        public ConcurrentBag<Interface> Results { get; } = new ConcurrentBag<Interface>();

        private static readonly HashSet<string> UiElementTypes = new HashSet<string>
        {
            "instantTextBoxType", "buttonType", "iconType", "background"
        };
        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                if (_insideGuiElement.Value == null) _insideGuiElement.Value = "none";

                string cleanKey = key?.Trim() ?? "";
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanValue = rawValueTrimmed.Trim('\"', '\'', '{', '}').Trim();

                if (depth == 0 && cleanKey == "guiTypes") return;

                // 1. Detect Container Start
                if (depth == 1 && cleanKey == "containerWindowType" && op == "=")
                {
                    _currentInterface.Value = new Interface { Type = cleanKey, FileName = fileName };
                    DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"STARTED NEW INTERFACE in {Path.GetFileName(fileName)}");
                    return;
                }

                var interfaceValue = _currentInterface.Value;
                if (interfaceValue == null) return;

                // 2. Detect Sub-Element Start
                if (depth == 2 && UiElementTypes.Contains(cleanKey) && op == "=")
                {
                    _insideGuiElement.Value = cleanKey;
                    // Create the sub-element object and assign a unique orderNumber for non-background elements
                    if (cleanKey == "background")
                    {
                        _currentBackground.Value = new Background();
                    }
                    else if (cleanKey == "instantTextBoxType")
                    {
                        // increment per-interface counter and assign order
                        interfaceValue.OrderCounter++;
                        _currentinstantTextBoxType.Value = new instantTextBoxType { orderNumber = interfaceValue.OrderCounter.ToString(CultureInfo.InvariantCulture) };
                    }
                    else if (cleanKey == "buttonType")
                    {
                        interfaceValue.OrderCounter++;
                        _currentbuttonType.Value = new buttonType { orderNumber = interfaceValue.OrderCounter.ToString(CultureInfo.InvariantCulture) };
                    }
                    else if (cleanKey == "iconType")
                    {
                        interfaceValue.OrderCounter++;
                        _currenticonType.Value = new iconType { orderNumber = interfaceValue.OrderCounter.ToString(CultureInfo.InvariantCulture) };
                    }

                    DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Entering Sub-Element: {cleanKey}");
                    return;
                }

                // 3. Detect Sub-Element End
                if (depth == 2 && cleanKey == "}")
                {
                    string type = _insideGuiElement.Value;
                    if (type == "background" && _currentBackground.Value != null) interfaceValue.Background.Add(_currentBackground.Value);
                    else if (type == "instantTextBoxType" && _currentinstantTextBoxType.Value != null) interfaceValue.instantTextBoxType.Add(_currentinstantTextBoxType.Value);
                    else if (type == "buttonType" && _currentbuttonType.Value != null) interfaceValue.buttonType.Add(_currentbuttonType.Value);
                    else if (type == "iconType" && _currenticonType.Value != null) interfaceValue.iconType.Add(_currenticonType.Value);

                    DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Exiting Sub-Element: {type} (Stored)");

                    _currentBackground.Value = null;
                    _currentinstantTextBoxType.Value = null;
                    _currentbuttonType.Value = null;
                    _currenticonType.Value = null;
                    _insideGuiElement.Value = "none";
                    return;
                }

                // 4. Handle Sub-Element Properties (Depth 3)
                if (_insideGuiElement.Value != "none" && depth == 3)
                {
                    HandleSubElementProperties(cleanKey, op, rawValueTrimmed, cleanValue, depth, fileName);
                    return;
                }

                // 5. Main Container Properties (Depth 2)
                if (depth == 2 && _insideGuiElement.Value == "none")
                {
                    if (cleanKey == "name") { interfaceValue.Id = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set Container ID: {cleanValue}"); }
                    else if (key == "moveable") { interfaceValue.moveable = true; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set Moveable: true"); }
                    else if (cleanKey == "position" || cleanKey == "size") HandleRegexPos(interfaceValue, cleanKey, cleanValue);
                    else if (key == "x") { interfaceValue.positionX = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set X: {cleanValue}"); }
                    else if (key == "y") { interfaceValue.positionY = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set Y: {cleanValue}"); }
                    else if (key == "width") { interfaceValue.sizeX = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set Width: {cleanValue}"); }
                    else if (key == "height") { interfaceValue.sizeY = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Set Height: {cleanValue}"); }
                    else if (cleanKey != "}")
                    {
                        var savedLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                        interfaceValue.Lines.Add(new ScriptedLine(savedLine, depth));
                    }
                }

                // 6. Close Main Container
                if (depth == 1 && cleanKey == "}")
                {
                    if (!string.IsNullOrEmpty(interfaceValue.Id))
                    {
                        Results.Add(interfaceValue);
                        DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"FINISHED & STORED INTERFACE: {interfaceValue.Id}");
                    }
                    _currentInterface.Value = null;
                }
            }
        }

        private void HandleSubElementProperties(string cleanKey, string op, string rawValueTrimmed, string cleanValue, int depth, string fileName)
        {
            string type = _insideGuiElement.Value;

            if (type == "background")
            {
                var obj = _currentBackground.Value;
                if (cleanKey == "name") { obj.Id = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"BG: name = {cleanValue}"); }
                else if (cleanKey == "quadTextureSprite") { obj.quadTextureSprite = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"BG: sprite = {cleanValue}"); }
                else SaveRawSubLine(obj.Lines, cleanKey, op, rawValueTrimmed, depth, fileName);
            }
            else if (type == "instantTextBoxType")
            {
                var obj = _currentinstantTextBoxType.Value;
                if (cleanKey == "name") { obj.Id = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: name = {cleanValue}"); }
                else if (cleanKey == "text") { obj.textLoc = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: loc = {cleanValue}"); }
                else if (cleanKey == "font") { obj.font = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: font = {cleanValue}"); }
                else if (cleanKey == "maxWidth") { obj.maxWidth = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: maxW = {cleanValue}"); }
                else if (cleanKey == "maxHeight") { obj.maxHeight = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: maxH = {cleanValue}"); }
                else if (cleanKey == "pdx_tooltip") { obj.pdx_tooltip = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: tooltip = {cleanValue}"); }
                else if (cleanKey == "position") { var p = ParsePoint(cleanValue); obj.positionX = p.X; obj.positionY = p.Y; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Text: Pos parsed {p.X},{p.Y}"); }
                else SaveRawSubLine(obj.Lines, cleanKey, op, rawValueTrimmed, depth, fileName);
            }
            else if (type == "buttonType")
            {
                var obj = _currentbuttonType.Value;
                if (cleanKey == "name") { obj.Id = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: name = {cleanValue}"); }
                else if (cleanKey == "buttonText") { obj.textLoc = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: text = {cleanValue}"); }
                else if (cleanKey == "font") { obj.font = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: font = {cleanValue}"); }
                else if (cleanKey == "spriteType") { obj.spriteType = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: sprite = {cleanValue}"); }
                else if (cleanKey == "scale") { obj.scale = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: scale = {cleanValue}"); }
                else if (cleanKey == "pdx_tooltip") { obj.pdx_tooltip = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: tooltip = {cleanValue}"); }
                else if (cleanKey == "position") { var p = ParsePoint(cleanValue); obj.positionX = p.X; obj.positionY = p.Y; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Button: Pos parsed {p.X},{p.Y}"); }
                else SaveRawSubLine(obj.Lines, cleanKey, op, rawValueTrimmed, depth, fileName);
            }
            else if (type == "iconType")
            {
                var obj = _currenticonType.Value;
                if (cleanKey == "name") { obj.Id = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: name = {cleanValue}"); }
                else if (cleanKey == "spriteType") { obj.spriteType = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: sprite = {cleanValue}"); }
                else if (cleanKey == "scale") { obj.scale = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: scale = {cleanValue}"); }
                else if (cleanKey == "pdx_tooltip") { obj.pdx_tooltip = cleanValue; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: tooltip = {cleanValue}"); }
                else if (cleanKey == "position") { var p = ParsePoint(cleanValue); obj.positionX = p.X; obj.positionY = p.Y; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: Pos parsed {p.X},{p.Y}"); }
                else if (cleanKey == "size") { var s = ParseSize(cleanValue); obj.sizeX = s.X; obj.sizeY = s.Y; DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Info, $"Icon: Size parsed {s.X},{s.Y}"); }
                else SaveRawSubLine(obj.Lines, cleanKey, op, rawValueTrimmed, depth, fileName);
            }
        }

        private void SaveRawSubLine(List<ScriptedLine> lines, string key, string op, string raw, int depth, string fileName)
        {
            var savedLine = RawLineHelper.BuildAndLog(key, op, raw, depth);
            lines.Add(new ScriptedLine(savedLine, depth));
            DebugLogger.Log("InterfaceImporter", fileName, LogLevel.Raw, $"Saved Raw Sub-Line (D{depth}): {savedLine}");
        }

        private (string X, string Y) ParsePoint(string val)
        {
            var x = Regex.Match(val, @"x\s*=\s*(-?\d+)");
            var y = Regex.Match(val, @"y\s*=\s*(-?\d+)");
            return (x.Success ? x.Groups[1].Value : "0", y.Success ? y.Groups[1].Value : "0");
        }

        private (string X, string Y) ParseSize(string val)
        {
            var w = Regex.Match(val, @"width\s*=\s*(\d+)");
            var h = Regex.Match(val, @"height\s*=\s*(\d+)");
            return (w.Success ? w.Groups[1].Value : "0", h.Success ? h.Groups[1].Value : "0");
        }

        private void HandleRegexPos(Interface inter, string key, string val)
        {
            if (key == "position")
            {
                var p = ParsePoint(val);
                inter.positionX = p.X;
                inter.positionY = p.Y;
                DebugLogger.Log("InterfaceImporter", inter.FileName, LogLevel.Info, $"Pos Regex: x={inter.positionX}, y={inter.positionY}");
            }
            else
            {
                var s = ParseSize(val);
                inter.sizeX = s.X;
                inter.sizeY = s.Y;
                DebugLogger.Log("InterfaceImporter", inter.FileName, LogLevel.Info, $"Size Regex: w={inter.sizeX}, h={inter.sizeY}");
            }
        }
    }
}