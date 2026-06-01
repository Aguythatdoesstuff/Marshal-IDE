using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Compiler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IEnumerable<string> filePaths = Array.Empty<string>();

            string outputArg = null;
            if (args != null && args.Length > 0)
            {
                outputArg = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
            }

            string resolvedOutputPath;
            if (!string.IsNullOrWhiteSpace(outputArg))
            {
                var raw = outputArg.Substring("--output=".Length).Trim();
                if (!Path.IsPathRooted(raw))
                {
                    Console.WriteLine("Error: --output must be an absolute path. Please provide an absolute path.");
                    return;
                }

                try
                {
                    resolvedOutputPath = Path.GetFullPath(raw);
                }
                catch
                {
                    Console.WriteLine("Error: Failed to normalize provided --output path. Provide a valid absolute path.");
                    return;
                }
            }
            else
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                resolvedOutputPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "compiled-data");
            }

            BaseCompiler.OutputPath = resolvedOutputPath;
            try
            {
                Directory.CreateDirectory(BaseCompiler.OutputPath);
            }
            catch { }


            // Remaining args (excluding the --output= arg) are treated as file paths.
            if (args != null && args.Length > 0)
            {
                var remaining = args.Where(a => !string.IsNullOrWhiteSpace(outputArg) ? !a.Equals(outputArg, StringComparison.OrdinalIgnoreCase) : true).ToArray();
                if (remaining.Length > 0)
                {
                    filePaths = remaining.Select(Path.GetFullPath).ToArray();
                }
                else
                {
                    // No file paths supplied, fallback to discovery of test input files
                    filePaths = DiscoverTestFiles();
                }
            }
            else
            {
                // No args at all: fallback to discovery of test input files
                filePaths = DiscoverTestFiles();
            }

            if (!filePaths.Any())
            {
                Console.WriteLine("No valid files to process. Exiting.");
                return;
            }

            var manager = new ProcessManager();
            manager.ProcessFiles(filePaths);
        }

        private static IEnumerable<string> DiscoverTestFiles()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var testDir = Path.Combine(desktop, "marshal-ide-compiler-debbug", "input");

            if (!Directory.Exists(testDir))
            {
                Console.WriteLine("Test directory not found: " + testDir);
                try
                {
                    Directory.CreateDirectory(testDir);
                    Console.WriteLine("Created test directory: " + testDir);
                }
                catch
                {
                    // ignore create failure, fall through to return
                }
                return Array.Empty<string>();
            }

            Console.WriteLine("Scanning test directory: " + testDir);

            var allowedExtensions = new[] { ".decision", ".event", ".focus", ".idea", ".scriptedgui", ".script" };
            var files = new List<string>();

            foreach (var ext in allowedExtensions)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + ext, SearchOption.AllDirectories));
            }

            Console.WriteLine($"Found {files.Count} valid file(s) in {testDir}\n");
            return files.Select(Path.GetFullPath).ToArray();
        }


    }
}