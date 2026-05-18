using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using static importer.ScriptedGuiImporter;

namespace importer
{
    public class ScriptedGuiImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "scripted_guis");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        // We still wait for Loc so the Compiler has a guarantee the dictionary is full
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public enum ConditionKind { If, ElseIf, Else }

        public class Gui
        {
            // The localisation key from inside a text block
            public string NameLocKey { get; set; }

            // Whether this text scriptedGui is an if / else if / else or unconditional
            public ConditionKind Condition { get; set; } = ConditionKind.Else;

            // Raw lines inside this text block (triggers, other raw content)
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class ScriptedGui
        {
            public string Id { get; set; } // e.g., enable_france
            public string ContextType { get; set; } // e.g., player_context
            public string Sprite { get; set; } // The ScriptedGui sprite
            public string WindowName { get; set; } // e.g., RULE_ENABLE_FRANCE
            public string FileName { get; set; }
            public Boolean IsDefault { get; set; }
            // Raw lines at the focus tree root (depth 1) that aren't otherwise parsed
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
            // Visible block raw lines (visible = { ... })
            public List<ScriptedLine> VisibleLines { get; } = new List<ScriptedLine>();

            // Properties block: multiple property entries each with an ID
            public List<Property> Properties { get; } = new List<Property>();

            // Effects block: multiple effect entries each with an ID
            public List<Effect> Effects { get; } = new List<Effect>();
        }

        public class Property
        {
            public string Id { get; set; }
            // Image location string if present (e.g. "[get_random_image_loc]")
            public string Image { get; set; }
            // Frame id if present (e.g. breach_progress)
            public string Frame { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class Effect
        {
            public string Id { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        private class BlockContext
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public int Depth { get; set; }
        }

        private readonly AsyncLocal<ScriptedGui> _currentScriptedGui = new AsyncLocal<ScriptedGui>();
        private readonly AsyncLocal<Gui> _currentGui = new AsyncLocal<Gui>();
        // Track whether we are inside visible / properties / effects blocks and their depths
        private readonly AsyncLocal<bool> _inVisible = new AsyncLocal<bool>();
        private readonly AsyncLocal<int?> _visibleDepth = new AsyncLocal<int?>();

        private readonly AsyncLocal<bool> _inProperties = new AsyncLocal<bool>();
        private readonly AsyncLocal<int?> _propertiesDepth = new AsyncLocal<int?>();
        private readonly AsyncLocal<Property> _currentProperty = new AsyncLocal<Property>();
        private readonly AsyncLocal<int?> _currentPropertyDepth = new AsyncLocal<int?>();

        private readonly AsyncLocal<bool> _inEffects = new AsyncLocal<bool>();
        private readonly AsyncLocal<int?> _effectsDepth = new AsyncLocal<int?>();
        private readonly AsyncLocal<Effect> _currentEffect = new AsyncLocal<Effect>();
        private readonly AsyncLocal<int?> _currentEffectDepth = new AsyncLocal<int?>();
        private readonly AsyncLocal<Stack<BlockContext>> _blockStack = new AsyncLocal<Stack<BlockContext>>();
        // (No prerequisite/mutually_exclusive handling in scripted localisation)
        public ConcurrentBag<ScriptedGui> Results { get; } = new ConcurrentBag<ScriptedGui>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanValue = rawValueTrimmed.Trim('\"', '\'');
                string cleanKey = key?.Trim() ?? "";
                if (depth == 0 && key == "scripted_gui" && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    return; // useless wrapper we dont care about
                }

                // 1. Root Level: Start of ScriptedGui (Depth 0)
                if (depth == 1 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    // depth==1 indicates the start of a new scripted gui entry (its id is the key)
                    var normalizedId = key?.Trim().Trim('"', '\'') ?? key;
                    _currentScriptedGui.Value = new ScriptedGui { Id = normalizedId, FileName = fileName };
                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"Started ScriptedGui id: {key}");
                    return;
                }

                var ScriptedGuiValue = _currentScriptedGui.Value;
                if (ScriptedGuiValue == null) return;

                // Ensure block stack exists for this async context
                if (_blockStack.Value == null) _blockStack.Value = new Stack<BlockContext>();

                // Global closing brace handling: if we have a block on stack and this token is the closing brace for it, pop and handle
                // Note: parser may report closing '}' with depth equal or decremented, treat any depth <= startDepth as closing
                    if (cleanKey == "}" && _blockStack.Value.Count > 0 && depth <= _blockStack.Value.Peek().Depth)
                {
                    var closed = _blockStack.Value.Pop();
                    switch (closed.Name)
                    {
                        case "visible":
                            _inVisible.Value = false;
                            _visibleDepth.Value = null;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting visible block");
                            break;
                        case "properties":
                            _inProperties.Value = false;
                            _propertiesDepth.Value = null;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting properties block");
                            break;
                        case "effects":
                            _inEffects.Value = false;
                            _effectsDepth.Value = null;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting effects block");
                            break;
                        case "property":
                            if (_currentProperty.Value != null && _currentProperty.Value.Id == closed.Id)
                            {
                                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"exiting property {_currentProperty.Value.Id}");
                                _currentProperty.Value = null;
                                _currentPropertyDepth.Value = null;
                            }
                            break;
                        case "effect":
                            if (_currentEffect.Value != null && _currentEffect.Value.Id == closed.Id)
                            {
                                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"exiting effect {_currentEffect.Value.Id}");
                                _currentEffect.Value = null;
                                _currentEffectDepth.Value = null;
                            }
                            break;
                    }

                    // We've handled the closing brace as a block closer; do not treat it as content for any block
                    return;
                }

                // 2. Root Level: End of ScriptedGui (Depth 0)
                // Because depth is decremented BEFORE the token is passed, the root '}' is at Depth 0
                    if (depth == 0 && cleanKey == "}")
                {
                    Results.Add(ScriptedGuiValue);
                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"Finished ScriptedGui: {ScriptedGuiValue.ContextType} with the id: {ScriptedGuiValue.Id}");
                    _currentScriptedGui.Value = null;
                    return;
                }

                var scriptedGui = _currentGui.Value;

                // Early handling for root-level block starts so they aren't swallowed by visible mode
                if (depth == 2 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    if (cleanKey == "properties")
                    {
                        // If we were in visible, exit it
                        if (_inVisible.Value)
                        {
                            _inVisible.Value = false;
                            _visibleDepth.Value = null;
                            if (_blockStack.Value.Count > 0 && _blockStack.Value.Peek().Name == "visible") _blockStack.Value.Pop();
                        }
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering properties block");
                        _inProperties.Value = true;
                        _propertiesDepth.Value = depth;
                        _blockStack.Value.Push(new BlockContext { Name = "properties", Depth = depth });
                        return;
                    }
                    if (cleanKey == "effects")
                    {
                        if (_inVisible.Value)
                        {
                            _inVisible.Value = false;
                            _visibleDepth.Value = null;
                            if (_blockStack.Value.Count > 0 && _blockStack.Value.Peek().Name == "visible") _blockStack.Value.Pop();
                        }
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering effects block");
                        _inEffects.Value = true;
                        _effectsDepth.Value = depth;
                        _blockStack.Value.Push(new BlockContext { Name = "effects", Depth = depth });
                        return;
                    }
                    if (cleanKey == "visible")
                    {
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering visible block");
                        _inVisible.Value = true;
                        _visibleDepth.Value = depth;
                        _blockStack.Value.Push(new BlockContext { Name = "visible", Depth = depth });
                        return;
                    }
                }

                // First, handle being inside a visible block
                if (_inVisible.Value)
                {
                    // Closing visible block: consider any closing '}' that brings depth below the start depth
                        if (_visibleDepth.Value.HasValue && depth <= _visibleDepth.Value.Value)
                    {
                        // We're at or above the visible block level; exit visible mode and continue processing this token
                        _inVisible.Value = false;
                        _visibleDepth.Value = null;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting visible block (synth)");
                    }
                    else
                    {
                        // If we encounter a properties/effects block start while in visible, switch to that block instead
                            if (op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{") && (cleanKey == "properties" || cleanKey == "effects"))
                        {
                            if (cleanKey == "properties")
                            {
                                DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "switching visible -> properties block");
                                _inVisible.Value = false;
                                _visibleDepth.Value = null;
                                _inProperties.Value = true;
                                _propertiesDepth.Value = depth;
                                _blockStack.Value.Push(new BlockContext { Name = "properties", Depth = depth });
                                return;
                            }
                            if (cleanKey == "effects")
                            {
                                DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "switching visible -> effects block");
                                _inVisible.Value = false;
                                _visibleDepth.Value = null;
                                _inEffects.Value = true;
                                _effectsDepth.Value = depth;
                                _blockStack.Value.Push(new BlockContext { Name = "effects", Depth = depth });
                                return;
                            }
                        }

                      

                        var vline = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                        ScriptedGuiValue.VisibleLines.Add(new ScriptedLine(vline, depth - 1)); // required for the scripted gui compiler to compile on correct indentation level
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"Visible Line (D{depth}): {vline}");
                        return;
                    }
                }

                // Handle properties block
                if (_inProperties.Value)
                {
                    // Closing the entire properties block: consider any closing '}' that brings depth below the start depth
                            if (cleanKey == "}" && _propertiesDepth.Value.HasValue && depth <= _propertiesDepth.Value.Value)
                    {
                        _inProperties.Value = false;
                        _propertiesDepth.Value = null;
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting properties block");
                        return;
                    }

                    // If we're currently inside a specific property entry
                    if (_currentProperty.Value != null)
                    {
                        // Closing current property entry: consider depth falling below the property's start depth
                        if (cleanKey == "}" && _currentPropertyDepth.Value.HasValue && depth <= _currentPropertyDepth.Value.Value)
                        {
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"exiting property {_currentProperty.Value.Id}");
                            _currentProperty.Value = null;
                            _currentPropertyDepth.Value = null;
                            return;
                        }

                        // Capture known property keys explicitly
                        if (op == "=" && cleanKey == "image")
                        {
                            _currentProperty.Value.Image = cleanValue;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"Property Image: {cleanValue} for {_currentProperty.Value.Id}");
                            return;
                        }
                        if (op == "=" && cleanKey == "frame")
                        {
                            _currentProperty.Value.Frame = cleanValue;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"Property Frame: {cleanValue} for {_currentProperty.Value.Id}");
                            return;
                        }

                        // Unexpected key inside a property - warn and save as raw line
                        // Ignore stray closing braces inside properties
                        

                        var pline = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Warning, $"Unexpected key in property {_currentProperty.Value.Id}: {cleanKey}");
                        _currentProperty.Value.Lines.Add(new ScriptedLine(pline, depth));
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"Property Line (D{depth}): {pline}");
                        return;
                    }

                    // Start of a new property entry (depth is propertiesDepth + 1)
                    if (_propertiesDepth.Value.HasValue && depth == _propertiesDepth.Value.Value + 1 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                    {
                        var prop = new Property { Id = cleanKey };
                        ScriptedGuiValue.Properties.Add(prop);
                        _currentProperty.Value = prop;
                        _currentPropertyDepth.Value = depth;
                        // Push property block to stack so we correctly detect its closing brace
                        _blockStack.Value.Push(new BlockContext { Name = "property", Id = cleanKey, Depth = depth });
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"entering property: {cleanKey}");
                        return;
                    }

                    // Otherwise ignore or store nothing at this level
                    return;
                }

                // Handle effects block similarly
                if (_inEffects.Value)
                {
                    // Closing effects block: depth is decremented before token is passed
                    if (cleanKey == "}" && _effectsDepth.Value.HasValue && depth <= _effectsDepth.Value.Value)
                    {
                        _inEffects.Value = false;
                        _effectsDepth.Value = null;
                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "exiting effects block");
                        return;
                    }

                    if (_currentEffect.Value != null)
                    {
                        // Closing effect entry: depth is decremented before token is passed
                        if (cleanKey == "}" && _currentEffectDepth.Value.HasValue && depth <= _currentEffectDepth.Value.Value)
                        {
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"exiting effect {_currentEffect.Value.Id}");
                            _currentEffect.Value = null;
                            _currentEffectDepth.Value = null;
                            return;
                        }

                        // Capture known effect keys explicitly: none for now, save as raw
                        // Ignore stray closing braces inside effects
                        

                        var eline = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                        _currentEffect.Value.Lines.Add(new ScriptedLine(eline, depth - 1));
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"Effect Line (D{depth}): {eline}");
                        return;
                    }

                    if (_effectsDepth.Value.HasValue && depth == _effectsDepth.Value.Value + 1 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                    {
                        var eff = new Effect { Id = cleanKey };
                        ScriptedGuiValue.Effects.Add(eff);
                        _currentEffect.Value = eff;
                        _currentEffectDepth.Value = depth - 1; // -1 so due to wrapper so that compiler compiles on correct indentation level
                        // Push effect block to stack so we correctly detect its closing brace
                        _blockStack.Value.Push(new BlockContext { Name = "effect", Id = cleanKey, Depth = depth });
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"entering effect: {cleanKey}");
                        return;
                    }
         
                    return;
                }

                if (scriptedGui == null)
                {
                    // Handle depth-1 special keys (id, default, opening a visible, properties and effects)
                    if (depth == 2)
                    {
                        // Some files use 'name' instead of 'id' for defined_text entries
                        if (cleanKey == "window_name" && op == "=")
                        {
                            ScriptedGuiValue.WindowName = cleanValue;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"ScriptedGui WindowName: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "context_type" && op == "=")
                        {
                            ScriptedGuiValue.ContextType = cleanValue;
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, $"ScriptedGui context_type: {cleanValue}");
                            return;
                        }

                        if (cleanKey == "visible" && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                        {
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering visible block");
                            _inVisible.Value = true;
                            _visibleDepth.Value = depth;
                            _blockStack.Value.Push(new BlockContext { Name = "visible", Depth = depth });
                            return;
                        }

                        if (cleanKey == "properties" && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                        {
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering properties block");
                            _inProperties.Value = true;
                            _propertiesDepth.Value = depth;
                            _blockStack.Value.Push(new BlockContext { Name = "properties", Depth = depth });
                            return;
                        }

                        if (cleanKey == "effects" && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                        {
                            DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Info, "entering effects block");
                            _inEffects.Value = true;
                            _effectsDepth.Value = depth;
                            _blockStack.Value.Push(new BlockContext { Name = "effects", Depth = depth });
                            return;
                        }
                    }

                   

                    // Ignore stray closing brace tokens at the root level - they are noise from parser depth quirks
                    if (cleanKey == "}")
                    {
                        DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, "Ignoring stray root closing brace");
                        return;
                    }

                    var rootLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    ScriptedGuiValue.Lines.Add(new ScriptedLine(rootLine, depth));
                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"Saved Root Line (D{depth}): {rootLine}");
                    return;
                }

                // 5. Construct and save the raw lines (triggers and other raw content).
                // Do not save 'localization_key' here because it's stored in NameLocKey.
                // Ignore stray closing braces saved as normal scripted gui lines

                // Ignore stray closing brace tokens that may be emitted by the parser
                if (cleanKey == "}")
                {
                    DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, "Ignoring stray closing brace");
                    return;
                }

                var savedLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                scriptedGui.Lines.Add(new ScriptedLine(savedLine, depth));
                DebugLogger.Log("ScriptedGuiImporter", fileName, LogLevel.Raw, $"Saved Line (D{depth}): {savedLine}");
                }
            }
        }
    }
