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
            // Build validator manager and register specialized validators
            var manager = new BaseValidatorManager();

            // Register specialized EffectsValidator for .scriptedeffect
            manager.RegisterValidator(".scriptedeffect", new EffectsValidator());

            IEnumerable<string> paths = args != null && args.Length > 0
                ? args.Select(a => a).ToArray()
                : DiscoverTestFiles();

            Console.WriteLine($"Validating {paths.Count()} files...");
            var ok = manager.TryValidateAndRun(paths, validFiles =>
            {
                Console.WriteLine("All files validated. Running compiler placeholder...");
                foreach (var f in validFiles) Console.WriteLine("-> " + f);
            });

            if (!ok) Environment.ExitCode = 1;
        }

        private static IEnumerable<string> DiscoverTestFiles()
        {
            // Default dev/test folder: Desktop/Compiler/testing
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var testDir = Path.Combine(desktop, "Compiler", "testing");
            if (!Directory.Exists(testDir)) return Array.Empty<string>();
            // Return all supported extensions files
            var exts = new[] { ".decision", ".event", ".scriptedgui", ".script", ".idea", ".focus", ".scriptedeffect" };
            var files = new List<string>();
            foreach (var e in exts)
            {
                files.AddRange(Directory.GetFiles(testDir, "*" + e, SearchOption.TopDirectoryOnly));
            }

            return files;
        }
    }
}
