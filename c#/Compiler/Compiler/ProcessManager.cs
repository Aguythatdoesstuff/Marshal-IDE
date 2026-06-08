using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Compiler
{
    public class ProcessManager
    {
        public List<ValidationError> AllErrors { get; private set; } = new List<ValidationError>();

        private static readonly string[] SupportedExtensions = new[]
        {
            ".decision",
            ".event",
            ".focus",
            ".idea",
            ".scriptedgui",
            ".script",
            ".dds"
        };

        public void ProcessFiles(IEnumerable<string> filePaths)
        {
            AllErrors.Clear();

            var tasks = new List<System.Threading.Tasks.Task>();
            foreach (var path in filePaths)
            {
                var p = path;
                // Wrap each file processing in a task that will capture exceptions and escalate fatal ones
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        ProcessSingleFileAsync(p);
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
            // Produce only a raw JSON report so external tools (JS) can parse it directly.
            if (!AllErrors.Any())
            {
                try
                {
                    var emptyReport = new { TotalErrors = 0, Files = new object[0] };
                    var emptyOptions = new JsonSerializerOptions { WriteIndented = true };
                    var emptyJson = JsonSerializer.Serialize(emptyReport, emptyOptions);
                    Compiler.Logging.Logger.LogMain(emptyJson);
                    // Send the structured object directly so the IPC serializer emits proper JSON (not a JSON string)
                    IPC.Send("ValidationReport", emptyReport);
                }
                catch { }
                return;
            }

            // Build a structured report grouped by file
            var report = AllErrors
                .GroupBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    File = g.Key,
                    Errors = g.Select(e => new { Line = e.LineNumber, Message = e.ErrorMessage }).ToList()
                })
                .ToList();

            var reportWrapper = new { TotalErrors = AllErrors.Count, Files = report };

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

        private void DispatchValidator(string absolutePath, string extension)
        {
            Compiler.Logging.Logger.LogMain($"Processing: {absolutePath}");

            BaseValidator validator = null;

            switch (extension)
            {
                case ".decision":
                    validator = new DecisionValidator();
                    break;
                case ".event":
                    validator = new EventValidator();
                    break;
                case ".focus":
                    validator = new FocusValidator();
                    break;
                case ".idea":
                    validator = new IdeaValidator();
                    break;
                case ".scriptedgui":
                    validator = new ScriptedGUIValidator();
                    break;
                case ".script":
                    validator = new ScriptValidator();
                    break;
                    default:
                    Compiler.Logging.Logger.LogMain("Unsupported file type: " + extension);
                    break;
            }

            // If there is no validator, allow certain raw file types (like .dds) to proceed
            if (validator != null)
            {
                validator.ValidateFile(absolutePath, Path.GetFileName(absolutePath));
                lock (AllErrors)
                {
                    AllErrors.AddRange(validator.Errors);
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
                hadErrors = AllErrors.Any(e => string.Equals(e.FileName, Path.GetFileName(absolutePath), StringComparison.OrdinalIgnoreCase));
            }

            if (!hadErrors)
            {
                try
                {
                    CompileFileForExtension(extension, absolutePath, validator);
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

        private void ProcessSingleFileAsync(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                Compiler.Logging.Logger.LogMain($"[ERROR] File does not exist: {absolutePath}");
                return;
            }

            string ext = Path.GetExtension(absolutePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
            {
                Compiler.Logging.Logger.LogMain($"[SKIP] Ignored unsupported file: {absolutePath}");
                return;
            }

            DispatchValidator(absolutePath, ext);
        }

        private void CompileFileForExtension(string extension, string absolutePath, BaseValidator validator)
        {
            BaseCompiler compiler = null;
            switch (extension)
            {
                case ".decision":
                    compiler = new Compiler.decision.DecisionCompiler();
                    break;
                case ".event":
                    compiler = new Compiler.@event.EventCompiler();
                    break;
                case ".idea":
                    compiler = new Compiler.idea.IdeaCompiler();
                    break;
                case ".focus":
                    compiler = new Compiler.focus.FocusCompiler();
                    break;
                case ".scriptedgui":
                    compiler = new Compiler.scriptedGui.ScriptedGUICompiler();
                    break;
                case ".script":
                    compiler = new Compiler.script.ScriptCompiler();
                    break;
                case ".dds":
                    compiler = new DdsCompiler();
                    break;
                default:
                    // No compiler implemented for this extension yet
                    return;
            }

            if (compiler != null)
            {
                // Provide the compiler with the source file name directly from the absolute path
                try
                {
                    compiler.SourceFileName = Path.GetFileName(absolutePath);
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

                        if (parserInstance is ScriptParser sParser && compiler is Compiler.script.ScriptCompiler sCompiler)
                        {
                            sCompiler.PassedData = sParser.LastParsedFile;
                        }
                        if (parserInstance is ScriptedGUIParser sgParser && compiler is Compiler.scriptedGui.ScriptedGUICompiler sgCompiler)
                        {
                            sgCompiler.PassedData = sgParser.LastParsedFile;
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
                    lock (AllErrors)
                    {
                        if (compiler is BaseCompiler bc && bc.CompilerErrors != null && bc.CompilerErrors.Count > 0)
                        {
                            AllErrors.AddRange(bc.CompilerErrors);
                        }
                    }
                }
                catch { }
                Compiler.Logging.Logger.LogComponent("Compiler", $"[COMPILER] Wrote output for {absolutePath} to {BaseCompiler.OutputPath}");
            }
        }
    }
}