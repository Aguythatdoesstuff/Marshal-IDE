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
            IEnumerable<string> filePaths;

            // Use absolute paths from arguments if spawned by the parent process
            if (args != null && args.Length > 0)
            {
                filePaths = args.Select(Path.GetFullPath).ToArray();
            }
            else
            {
                // Fallback to manual local testing
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
            var testDir = Path.Combine(desktop, "Compiler testing");

            if (!Directory.Exists(testDir))
            {
                Console.WriteLine("Test directory not found: " + testDir);
                return Array.Empty<string>();
            }

            Console.WriteLine("Scanning test directory: " + testDir);

            // Strictly restricted to the exact extensions from the image
            var allowedExtensions = new[] { ".decision", ".event", ".focus", ".idea", ".scriptedgui", ".script" };
            var files = new List<string>();

            foreach (var ext in allowedExtensions)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + ext, SearchOption.AllDirectories));
            }

            Console.WriteLine($"Found {files.Count} valid file(s) in {testDir}\n");
            return files;
        }
    }
}