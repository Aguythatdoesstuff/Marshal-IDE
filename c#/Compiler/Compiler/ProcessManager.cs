using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            ".script"
        };

        public void ProcessFiles(IEnumerable<string> filePaths)
        {
            AllErrors.Clear();

            var tasks = new List<System.Threading.Tasks.Task>();
            foreach (var path in filePaths)
            {
                var p = path;
                tasks.Add(System.Threading.Tasks.Task.Run(() => ProcessSingleFileAsync(p)));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            PrintTemporaryErrors();
        }
        private void PrintTemporaryErrors()
        {
            Console.WriteLine("\n--- VALIDATION TEST RESULTS ---");
            if (!AllErrors.Any())
            {
                Console.WriteLine("No errors found! Compilation clear.");
                return;
            }

            foreach (var err in AllErrors)
            {
                Console.WriteLine($"[ERROR] {err.FileName} (Line {err.LineNumber}): {err.ErrorMessage}");
            }
            Console.WriteLine($"Total Errors: {AllErrors.Count}\n-------------------------------");
        }

        private void DispatchValidator(string absolutePath, string extension)
        {
            Console.WriteLine($"Processing: {absolutePath}");

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
                    Console.WriteLine("Unsupported file type: " + extension);
                    break;
            }

            if (validator == null) return;

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

            // Only attempt compilation if no validation or parser errors were produced
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
                    Console.WriteLine($"Compiler failed for {absolutePath}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Skipping compilation for {absolutePath} due to errors.");
            }
        }

        private void ProcessSingleFileAsync(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                Console.WriteLine($"[ERROR] File does not exist: {absolutePath}");
                return;
            }

            string ext = Path.GetExtension(absolutePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
            {
                Console.WriteLine($"[SKIP] Ignored unsupported file: {absolutePath}");
                return;
            }

            DispatchValidator(absolutePath, ext);
        }

        private void CompileFileForExtension(string extension, string absolutePath, BaseValidator validator)
        {
            BaseCompiler compiler = null;
            switch (extension)
            {
                case ".event":
                    compiler = new Compiler.@event.EventCompiler();
                    break;
                default:
                    // No compiler implemented for this extension yet
                    return;
            }

            if (compiler != null)
            {
                // Give the compiler the parser-provided source file name when available
                try
                {
                    var parserErrors = validator.GetParserErrors(); // forces access
                    // Many validators provide Parser which may have SourceFileName set
                    var parserField = validator?.GetType()?.GetProperty("Parser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                    if (parserField != null)
                    {
                        var parser = parserField.GetValue(validator) as BaseParser;
                        if (parser != null && !string.IsNullOrWhiteSpace(parser.SourceFileName))
                        {
                            compiler.SourceFileName = parser.SourceFileName;
                        }
                    }
                }
                catch
                {
                    // ignore reflection failures
                }

                compiler.Compile(absolutePath);
                Console.WriteLine($"[COMPILER] Wrote output for {absolutePath} to {BaseCompiler.OutputPath}");
            }
        }
    }
}