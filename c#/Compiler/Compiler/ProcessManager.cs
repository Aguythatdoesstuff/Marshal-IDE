using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Compiler.history;

namespace Compiler
{
    public class ProcessManager
    {
        public List<ValidationError> AllErrors { get; private set; } = new List<ValidationError>();
        public List<ValidationWarning> AllWarnings { get; private set; } = new List<ValidationWarning>();

        // Store compilation options for the current processing run
        private Dictionary<string, CompilationOptions> _compilationOptionsMap = new Dictionary<string, CompilationOptions>(StringComparer.OrdinalIgnoreCase);

        // Track AST output results: file path -> (success, error message if failed)
        private Dictionary<string, (bool success, string errorMessage)> _astOutputResults = new Dictionary<string, (bool, string)>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] SupportedExtensions = new[]
        {
            ".decision",
            ".equipment",
            ".event",
            ".focus",
            ".idea",
            ".unit",
            ".scriptedgui",
            ".script",
            ".oob",
            ".history",
            ".dds"

        };

        /// <summary>
        /// Processes files with compilation options (new interface supporting AST output).
        /// </summary>
        public void ProcessFiles(IEnumerable<CompilationOptions> options)
        {
            // Reset per-run state so output files are treated as not-yet-created
            // at the start of each processing pass. This ensures the first write
            // to a given output path will truncate/create the file rather than
            // append to content produced by a prior run in the same process.
            BaseCompiler.ResetCreatedOutputFiles();

            AllErrors.Clear();
            AllWarnings.Clear();
            _compilationOptionsMap.Clear();
            _astOutputResults.Clear();

            var optionsList = options.ToList();
            Compiler.Logging.Logger.LogMain($"[ProcessFiles] Received {optionsList.Count} files to process");
            foreach (var opt in optionsList)
            {
                Compiler.Logging.Logger.LogMain($"[ProcessFiles] File: {opt.FilePath}");
                if (opt.ShouldOutputAst)
                {
                    Compiler.Logging.Logger.LogMain($"[ProcessFiles]   AST Output: {(opt.OnlyOutputAst ? "ONLY_AST" : "WITH_COMPILATION")} -> {opt.AstOutputPath}");
                }

                // Map file paths to their options for quick lookup during processing
                _compilationOptionsMap[opt.FilePath] = opt;
            }

            var tasks = new List<System.Threading.Tasks.Task>();
            foreach (var opt in optionsList)
            {
                var options_copy = opt; // Capture for closure
                // Wrap each file processing in a task that will capture exceptions and escalate fatal ones
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        ProcessSingleFileAsync(options_copy);
                    }
                    catch (Exception ex)
                    {
                        // Treat unexpected exceptions as fatal: report via IPC and logger, then rethrow
                        try
                        {
                            IPC.Send("FatalError", ex.Message);
                            Compiler.Logging.Logger.ReportUnhandledException(ex);
                        }
                        catch { }
                        throw;
                    }
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            PrintTemporaryErrors();
        }

        private void PrintTemporaryErrors()
        {
            // Build AST output results info
            var astOutputInfo = new List<object>();
            lock (_astOutputResults)
            {
                foreach (var kvp in _astOutputResults)
                {
                    astOutputInfo.Add(new
                    {
                        file = kvp.Key,
                        ast_output_success = kvp.Value.success,
                        ast_output_error = kvp.Value.errorMessage
                    });
                }
            }

            // Produce only a raw JSON report so external tools (JS) can parse it directly.
            if (!AllErrors.Any() && !AllWarnings.Any() && !astOutputInfo.Any())
            {
                try
                {
                    var emptyReport = new { TotalErrors = 0, TotalWarnings = 0, Files = new object[0], AstOutputResults = new object[0] };
                    var emptyOptions = new JsonSerializerOptions { WriteIndented = true };
                    var emptyJson = JsonSerializer.Serialize(emptyReport, emptyOptions);
                    Compiler.Logging.Logger.LogMain(emptyJson);
                    // Send the structured object directly so the IPC serializer emits proper JSON (not a JSON string)
                    IPC.Send("ValidationReport", emptyReport);
                }
                catch { }
                return;
            }

            // Build a structured report grouped by file (include warnings)
            var fileNames = AllErrors.Select(e => e.FileName).Concat(AllWarnings.Select(w => w.FileName)).Distinct(StringComparer.OrdinalIgnoreCase);

            var report = fileNames
                .Select(fn => new
                {
                    File = fn,
                    Errors = AllErrors.Where(e => string.Equals(e.FileName, fn, StringComparison.OrdinalIgnoreCase))
                                        .Select(e => new { line = e.LineNumber, message = e.ErrorMessage }).ToList(),
                    Warnings = AllWarnings.Where(w => string.Equals(w.FileName, fn, StringComparison.OrdinalIgnoreCase))
                                        .Select(w => new { line = w.LineNumber, message = w.WarningMessage }).ToList()
                })
                .ToList();

            var reportWrapper = new { TotalErrors = AllErrors.Count, TotalWarnings = AllWarnings.Count, Files = report, AstOutputResults = astOutputInfo };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(reportWrapper, options);

            // Log the structured JSON report instead of line-by-line text
            Compiler.Logging.Logger.LogMain(json);
            try
            {
                // Send the structured object directly so IPC will serialize it as JSON (not as an escaped string)
                IPC.Send("ValidationReport", reportWrapper);
            }
            catch { }
        }

        private void DispatchValidator(string absolutePath, string extension, CompilationOptions options = null)
        {
            string fileName = Path.GetFileName(absolutePath);
            Compiler.Logging.Logger.LogMain($"Processing: {absolutePath}");
            Compiler.Logging.Logger.LogComponentReceived(extension, absolutePath);

            BaseValidator validator = null;
            string selectedValidator = "NONE";

            switch (extension)
            {
                case ".decision":
                    validator = new DecisionValidator();
                    selectedValidator = "DecisionValidator";
                    break;
                case ".equipment":
                    validator = new EquipmentValidator();
                    selectedValidator = "EquipmentValidator";
                    break;
                case ".unit":
                    validator = new UnitValidator();
                    selectedValidator = "UnitValidator";
                    break;
                case ".event":
                    validator = new EventValidator();
                    selectedValidator = "EventValidator";
                    break;
                case ".focus":
                    validator = new FocusValidator();
                    selectedValidator = "FocusValidator";
                    break;
                case ".oob":
                    validator = new OobValidator();
                    selectedValidator = "OobValidator";
                    break;
                case ".idea":
                    validator = new IdeaValidator();
                    selectedValidator = "IdeaValidator";
                    break;
                case ".history":
                    validator = new HistoryValidator();
                    selectedValidator = "HistoryValidator";
                    break;
                case ".scriptedgui":
                    validator = new ScriptedGUIValidator();
                    selectedValidator = "ScriptedGUIValidator";
                    break;
                case ".script":
                    validator = new ScriptValidator();
                    selectedValidator = "ScriptValidator";
                    break;
                default:
                    Compiler.Logging.Logger.LogMain("Unsupported file type: " + extension);
                    Compiler.Logging.Logger.LogWarning("DISPATCHER", $"Extension {extension} is not supported");
                    IPC.Send("UnsupportedFile", new { file = absolutePath, extension = extension, path = absolutePath });
                    break;
            }

            if (validator != null)
            {
                Compiler.Logging.Logger.LogComponentSelected("VALIDATION", selectedValidator, absolutePath);
                Compiler.Logging.Logger.LogComponentProcessing("VALIDATION", selectedValidator, absolutePath);

                validator.ValidateFile(absolutePath, fileName);

                int errorCount = validator.Errors?.Count ?? 0;
                int warningCount = validator.Warnings?.Count ?? 0;
                int parserErrorCount = validator.GetParserErrors()?.Count() ?? 0;

                Compiler.Logging.Logger.LogComponentCompleted("VALIDATION", selectedValidator, absolutePath, 
                    errorCount == 0 && parserErrorCount == 0,
                    $"Errors={errorCount} Warnings={warningCount} ParserErrors={parserErrorCount}");

                lock (AllErrors)
                {
                    AllErrors.AddRange(validator.Errors);
                    // Collect warnings produced by the validator so they can be reported back to the caller
                    AllWarnings.AddRange(validator.Warnings);
                    var parserErrors = validator.GetParserErrors();
                    foreach (var pe in parserErrors)
                    {
                        AllErrors.Add(new ValidationError(pe.FileName, pe.LineNumber, "[PARSER] " + pe.ErrorMessage));
                    }
                }
            }

            // Only attempt compilation if no validation or parser errors were produced for this file
            bool hadErrors;
            lock (AllErrors)
            {
                hadErrors = AllErrors.Any(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            }

            // Handle AST output if requested
            if (options?.ShouldOutputAst == true)
            {
                if (hadErrors)
                {
                    string message = $"[AST Output] Skipping AST output for {absolutePath} due to validation errors.";
                    Compiler.Logging.Logger.LogMain(message);
                    RecordAstOutputFailure(absolutePath, "Validation errors prevent AST output");
                    // Send immediate IPC notification that AST output failed
                    IPC.Send("AstOutputFailure", new
                    {
                        file = absolutePath,
                        reason = "Validation errors detected",
                        errorCount = AllErrors.Count(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    });
                }
                else
                {
                    HandleAstOutput(absolutePath, validator, options);
                }
            }

            // Skip compilation if only AST output is requested
            if (options?.OnlyOutputAst == true)
            {
                Compiler.Logging.Logger.LogMain($"[AST Output] Compilation skipped (only_out_ast mode) for {absolutePath}");
                return;
            }

            if (!hadErrors)
            {
                try
                {
                    CompileFileForExtension(extension, absolutePath, validator, options);
                }
                catch (Exception ex)
                {
                    Compiler.Logging.Logger.LogMain($"Compiler failed for {absolutePath}: {ex.Message}");
                    // Escalate fatal compiler errors via IPC and logger and rethrow to allow top-level handlers to run
                    try
                    {
                        IPC.Send("FatalError", $"Compiler failed for {absolutePath}: {ex.Message}");
                        Compiler.Logging.Logger.ReportUnhandledException(ex);
                    }
                    catch { }
                    throw;
                }
            }
            else
            {
                Compiler.Logging.Logger.LogMain($"Skipping compilation for {absolutePath} due to errors.");
            }
        }

        /// <summary>
        /// Handles AST output for a validated file.
        /// </summary>
        private void HandleAstOutput(string absolutePath, BaseValidator validator, CompilationOptions options)
        {
            try
            {
                if (validator == null)
                {
                    string message = $"[AST Output] Validator is null; cannot output AST for {absolutePath}";
                    Compiler.Logging.Logger.LogMain(message);
                    RecordAstOutputFailure(absolutePath, message);
                    return;
                }

                // Extract AST from the parser
                var ast = AstOutputHelper.GetAstFromValidator(validator);
                if (ast == null)
                {
                    string message = $"[AST Output] No AST data available for {absolutePath}";
                    Compiler.Logging.Logger.LogMain(message);
                    RecordAstOutputFailure(absolutePath, message);
                    return;
                }

                // Get parser errors for inclusion in the output
                var parserErrors = validator.GetParserErrors();

                // Wrap the AST with metadata
                var wrappedAst = AstOutputHelper.WrapAstOutput(ast, absolutePath, parserErrors);

                // Write to JSON file
                bool success = AstOutputHelper.WriteAstToJson(wrappedAst, options.AstOutputPath);

                if (success)
                {
                    Compiler.Logging.Logger.LogMain($"[AST Output] Successfully wrote AST to {options.AstOutputPath}");
                    RecordAstOutputSuccess(absolutePath);

                    // Send immediate IPC notification that AST was successfully output
                    IPC.Send("AstOutputSuccess", new
                    {
                        file = absolutePath,
                        astPath = options.AstOutputPath
                    });
                }
                else
                {
                    string message = $"[AST Output] Failed to write AST to {options.AstOutputPath}";
                    Compiler.Logging.Logger.LogMain(message);
                    RecordAstOutputFailure(absolutePath, message);
                }
            }
            catch (Exception ex)
            {
                string message = $"[AST Output] Error handling AST output: {ex.Message}";
                Compiler.Logging.Logger.LogMain(message);
                RecordAstOutputFailure(absolutePath, message);
            }
        }

        /// <summary>
        /// Records a successful AST output operation.
        /// </summary>
        private void RecordAstOutputSuccess(string filePath)
        {
            lock (_astOutputResults)
            {
                _astOutputResults[filePath] = (true, null);
            }
        }

        /// <summary>
        /// Records a failed AST output operation with the error message.
        /// </summary>
        private void RecordAstOutputFailure(string filePath, string errorMessage)
        {
            lock (_astOutputResults)
            {
                _astOutputResults[filePath] = (false, errorMessage);
            }
        }

        private void ProcessSingleFileAsync(CompilationOptions options)
        {
            string absolutePath = options.FilePath;
            Compiler.Logging.Logger.LogMain($"[ProcessSingleFileAsync] Received file: {absolutePath}");

            if (!File.Exists(absolutePath))
            {
                Compiler.Logging.Logger.LogMain($"[ERROR] File does not exist: {absolutePath}");
                IPC.Send("FileNotFound", new { file = absolutePath, path = absolutePath });
                return;
            }

            string ext = Path.GetExtension(absolutePath).ToLowerInvariant();
            Compiler.Logging.Logger.LogMain($"[ProcessSingleFileAsync] File extension: {ext}");

            if (!SupportedExtensions.Contains(ext))
            {
                Compiler.Logging.Logger.LogMain($"[SKIP] Ignored unsupported file: {absolutePath}. Extension '{ext}' not in SupportedExtensions");
                IPC.Send("UnsupportedFile", new { file = absolutePath, extension = ext, path = absolutePath });
                return;
            }

            Compiler.Logging.Logger.LogMain($"[ProcessSingleFileAsync] Extension is supported, dispatching validator...");
            DispatchValidator(absolutePath, ext, options);
        }

        private void CompileFileForExtension(string extension, string absolutePath, BaseValidator validator, CompilationOptions options = null)
        {
            BaseCompiler compiler = null;
            string selectedCompiler = "NONE";

            switch (extension)
            {
                case ".decision":
                    compiler = new Compiler.decision.DecisionCompiler();
                    selectedCompiler = "DecisionCompiler";
                    break;
                case ".equipment":
                    compiler = new EquipmentCompiler();
                    selectedCompiler = "EquipmentCompiler";
                    break;
                case ".unit":
                    compiler = new UnitCompiler();
                    selectedCompiler = "UnitCompiler";
                    break;
                case ".oob":
                    compiler = new OobCompiler();
                    selectedCompiler = "OobCompiler";
                    break;
                case ".event":
                    compiler = new Compiler.@event.EventCompiler();
                    selectedCompiler = "EventCompiler";
                    break;
                case ".idea":
                    compiler = new Compiler.idea.IdeaCompiler();
                    selectedCompiler = "IdeaCompiler";
                    break;
                case ".focus":
                    compiler = new Compiler.focus.FocusCompiler();
                    selectedCompiler = "FocusCompiler";
                    break;
                case ".history":
                    compiler = new Compiler.history.HistoryCompiler();
                    selectedCompiler = "HistoryCompiler";
                    break;
                case ".scriptedgui":
                    compiler = new Compiler.scriptedGui.ScriptedGUICompiler();
                    selectedCompiler = "ScriptedGUICompiler";
                    break;
                case ".script":
                    compiler = new Compiler.script.ScriptCompiler();
                    selectedCompiler = "ScriptCompiler";
                    break;
                case ".dds":
                    compiler = new DdsCompiler();
                    selectedCompiler = "DdsCompiler";
                    break;
                default:
                    // No compiler implemented for this extension yet
                    Compiler.Logging.Logger.LogWarning("COMPILER", $"No compiler implemented for extension {extension}");
                    IPC.Send("NoCompiler", new { file = absolutePath, extension = extension, path = absolutePath });
                    return;
            }

            if (compiler != null)
            {
                string fileName = Path.GetFileName(absolutePath);
                Compiler.Logging.Logger.LogComponentSelected("COMPILATION", selectedCompiler, absolutePath);
                Compiler.Logging.Logger.LogComponentProcessing("COMPILATION", selectedCompiler, absolutePath);

                // Provide the compiler with the source file name directly from the absolute path
                try
                {
                    compiler.SourceFileName = fileName;
                    compiler.SourceAbsolutePath = absolutePath;
                }
                catch
                {
                    // ignore any unexpected failures
                }

                // If the validator ran a parser, attempt to hand parsed data to the compiler
                try
                {
                    var parserInstance = validator?.GetParserInstance();
                    if (parserInstance == null)
                    {
                        // No parser available (expected for raw files like .dds). Skip binding.
                    }
                    else
                    {
                        if (parserInstance is EventParser evParser && compiler is Compiler.@event.EventCompiler evCompiler)
                        {
                            evCompiler.PassedData = evParser.LastParsedFile;
                        }

                        if (parserInstance is IdeaParser iParser && compiler is Compiler.idea.IdeaCompiler iCompiler)
                        {
                            iCompiler.PassedData = iParser.LastParsedFile;
                        }
                        if (parserInstance is FocusParser fParser && compiler is Compiler.focus.FocusCompiler fCompiler)
                        {
                            // pass the parsed file (file name + trees) to the focus compiler
                            fCompiler.PassedTrees = fParser.LastParsedFile;
                        }
                        if (parserInstance is DecisionParser dParser && compiler is Compiler.decision.DecisionCompiler dCompiler)
                        {
                            dCompiler.PassedData = dParser.LastParsedFile;
                        }

                        if (parserInstance is EquipmentParser eqParser && compiler is EquipmentCompiler eqCompiler)
                        {
                            eqCompiler.PassedData = eqParser.LastParsedFile;
                        }

                        if (parserInstance is UnitParser uParser && compiler is UnitCompiler uCompiler)
                        {
                            uCompiler.PassedData = uParser.LastParsedFile;
                        }

                        if (parserInstance is ScriptParser sParser && compiler is Compiler.script.ScriptCompiler sCompiler)
                        {
                            sCompiler.PassedData = sParser.LastParsedFile;
                        }

                        if (parserInstance is OobParser oParser && compiler is OobCompiler oCompiler)
                        {
                            oCompiler.PassedData = oParser.LastParsedFile;
                        }

                        if (parserInstance is ScriptedGUIParser sgParser && compiler is Compiler.scriptedGui.ScriptedGUICompiler sgCompiler)
                        {
                            sgCompiler.PassedData = sgParser.LastParsedFile;
                        }

                        if (parserInstance is HistoryParser hParser && compiler is Compiler.history.HistoryCompiler hCompiler)
                        {
                            hCompiler.PassedData = hParser.LastParsedFile;
                        }
                    }
                }
                catch
                {
                    // non-fatal, proceed to compilation without attaching parsed data
                }

                compiler.Compile();
                // Collect non-fatal compiler errors produced during compilation
                try
                {
                    int compilerErrorCount = 0;
                    lock (AllErrors)
                    {
                        if (compiler is BaseCompiler bc && bc.CompilerErrors != null && bc.CompilerErrors.Count > 0)
                        {
                            AllErrors.AddRange(bc.CompilerErrors);
                            compilerErrorCount = bc.CompilerErrors.Count;
                        }
                    }
                    Compiler.Logging.Logger.LogComponentCompleted("COMPILATION", selectedCompiler, absolutePath, true, $"CompilerErrors={compilerErrorCount}");
                }
                catch { }
                Compiler.Logging.Logger.LogComponent("Compiler", $"[COMPILER] Wrote output for {absolutePath} to {BaseCompiler.OutputPath}");
            }
        }
    }
}