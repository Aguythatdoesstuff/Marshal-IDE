using System;
using System.IO;

namespace Compiler
{
    public abstract class BaseCompiler
    {
        // Tracks files that have already been created by any compiler during this
        // run. The first write to a given output path will truncate/create the
        // file; subsequent writes will append. This is static so multiple
        // compiler instances writing the same output file behave consistently.
        private static readonly object _writtenFilesLock = new object();
        private static readonly HashSet<string> _createdOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static string OutputPath { get; set; }
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// Creates or overwrites a file, providing a StreamWriter to cleanly dump data 
        /// directly to disk without storing massive strings in RAM. Works natively on Windows and Linux.
        /// </summary>
        protected void WriteFile(string relativeSubfolder, string fileName, string extension, Action<StreamWriter> writeAction)
        {
            if (writeAction == null) return;

            if (string.IsNullOrWhiteSpace(extension)) extension = ".txt";
            if (!extension.StartsWith(".")) extension = "." + extension;

            string finalName = fileName;
            if (string.IsNullOrWhiteSpace(finalName))
            {
                if (!string.IsNullOrWhiteSpace(SourceFileName))
                {
                    finalName = Path.GetFileNameWithoutExtension(SourceFileName);
                }
                else
                {
                    throw new InvalidOperationException("Fatal error: Unable to determine output file name.");
                }
            }

            var subfolder = string.IsNullOrWhiteSpace(relativeSubfolder) ? OutputPath : Path.Combine(OutputPath, relativeSubfolder);
            Directory.CreateDirectory(subfolder);
            var outPath = Path.Combine(subfolder, finalName + extension);

            bool shouldCreate = false;
            lock (_writtenFilesLock)
            {
                if (!_createdOutputFiles.Contains(outPath))
                {
                    shouldCreate = true;
                    _createdOutputFiles.Add(outPath);
                }
            }

            var fileMode = shouldCreate ? FileMode.Create : FileMode.Append;

            // Golden Rule Check: ONLY .yml files are allowed to have a BOM encoding!
            bool useBOM = extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
            var encoding = new System.Text.UTF8Encoding(useBOM);

            using (var fs = new FileStream(outPath, fileMode, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, encoding))
            {
                writeAction(sw);
            }
        }

        public static string Ident(int level)
        {
            if (level <= 0) return string.Empty;

            int spaceCount = level * 4;

            // Fast path for common indentation depths to bypass allocation completely
            return spaceCount switch
            {
                4 => "    ",
                8 => "        ",
                12 => "            ",
                16 => "                ",
                // Fallback that builds the exact string size perfectly in memory without concatenation loops
                _ => string.Create(spaceCount, spaceCount, (span, count) => span.Fill(' '))
            };
        }

        /* ============================================================================
           EXAMPLE USAGE INSIDE A SPECIALIZED COMPILER
           ============================================================================

           public class ScriptedGuiCompiler : BaseCompiler
           {
               // Assume this data was handed down after the Validator/Parser dropped old lists
               public ScriptedGUI PassedData { get; set; } 

               public override void Compile()
               {
                   // 1. Call WriteFile and pass a lambda expression 'sw => { ... }'
                   WriteFile("common/scripted_gui", "my_mod_gui", ".txt", sw =>
                   {
                       // Write the initial structural header
                       sw.WriteLine("scripted_gui = {");

                       // 2. Loop through your parsed data structures
                       foreach (var window in PassedData.Windows)
                       {
                           // 3. Use .NET 10 Raw String Literals (""") to easily output multi-line 
                           // templates from a single input element without clogging your RAM.
                           sw.Write($"""
                       	        {window.Id} = {{
                       		        size = {{ x = {window.Size?.x ?? 500} y = {window.Size?.y ?? 400} }}
                       		        reset_on_closure = yes
                       	        }}
                               """);

                           // You can mix single lines and raw string blocks perfectly. 
                           // The StreamWriter buffers it all automatically before touching the SSD.
                           if (window.Draggable)
                           {
                               sw.WriteLine("\t\tmoveable = yes");
                           }
                       }

                       // Close the core structure
                       sw.WriteLine("}");
                   }); 
                   // At this closing parenthesis, the file stream is flushed, closed, and saved to disk.
               }
           }
        */

        public abstract void Compile();
    }
}
