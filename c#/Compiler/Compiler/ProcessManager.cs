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
                    break;

                case ".event":
                    Console.WriteLine(" -> Routed to Event validator logic");
                    break;

                case ".focus":
                    Console.WriteLine(" -> Routed to Focus validator logic");
                    break;

                case ".idea":
                    Console.WriteLine(" -> Routed to Idea validator logic");
                    break;

                case ".scriptedgui":
                    Console.WriteLine(" -> Routed to Scripted GUI validator logic");
                    break;

                case ".script":
                    Console.WriteLine(" -> Routed to Script validator logic");
                    break;

                default:
                    Console.WriteLine(" -> No validator hooked up for this type yet.");
                    break;
            }

            if (validator != null)
            {
                validator.ValidateFile(absolutePath);
                AllErrors.AddRange(validator.Errors);
            }
        }
    }
}