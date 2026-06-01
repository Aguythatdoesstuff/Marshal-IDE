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

            foreach (var path in filePaths)
            {
                {
                    if (!File.Exists(path))
                    {
                        Console.WriteLine($"[ERROR] File does not exist: {path}");
                        continue;
                    }

                    string ext = Path.GetExtension(path).ToLowerInvariant();

                    if (!SupportedExtensions.Contains(ext))
                    {
                        Console.WriteLine($"[SKIP] Ignored unsupported file: {path}");
                        continue;
                    }

                    DispatchValidator(path, ext);
                }

                PrintTemporaryErrors();
            }
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
                    Console.WriteLine(" -> Routed to Decision validator logic");
                    validator = new DecisionValidator();
                    break;

                case ".event":
                    Console.WriteLine(" -> Routed to Event validator logic");
                    validator = new EventValidator();
                    break;

                case ".focus":
                    Console.WriteLine(" -> Routed to Focus validator logic");
                    validator = new FocusValidator();
                    break;

                case ".idea":
                    Console.WriteLine(" -> Routed to Idea validator logic");
                    validator = new IdeaValidator();
                    break;

                case ".scriptedgui":
                    Console.WriteLine(" -> Routed to Scripted GUI validator logic");
                    validator = new ScriptedGUIValidator();
                    break;

                case ".script":
                    Console.WriteLine(" -> Routed to Script validator logic");
                    validator = new ScriptValidator();
                    break;

                default:
                    Console.WriteLine(" -> Unsupported file type");
                    break;
            }

                if (validator != null)
                {
                    validator.ValidateFile(absolutePath, Path.GetFileName(absolutePath));
                    AllErrors.AddRange(validator.Errors);

                    // Append parser errors too (if any). Parser errors are kept on
                    // the parser instance itself to keep them separate from
                    // validation errors.
                    var parserErrors = validator.GetParserErrors();
                    foreach (var pe in parserErrors)
                    {
                        AllErrors.Add(new ValidationError(pe.FileName, pe.LineNumber, "[PARSER] " + pe.ErrorMessage));
                    }
                }
        }
    }
}