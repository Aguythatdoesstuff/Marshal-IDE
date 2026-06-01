using System;
using System.IO;

namespace Compiler
{
    public abstract class BaseCompiler
    {
        public static string OutputPath { get; set; }
        public string SourceFileName { get; set; } = string.Empty;

        public BaseCompiler()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(OutputPath))
                {
                    Directory.CreateDirectory(OutputPath);
                }
            }
            catch { }
        }


        protected void WriteFile(string relativeSubfolder, string fileName, string extension, string contents)
        {
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

            var subfolder = string.IsNullOrWhiteSpace(relativeSubfolder) ? BaseCompiler.OutputPath : Path.Combine(BaseCompiler.OutputPath, relativeSubfolder);
            Directory.CreateDirectory(subfolder);
            var outPath = Path.Combine(subfolder, finalName + extension);

            // Use StreamWriter to write output and overwrite existing files
            using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
            {
                sw.Write(contents ?? string.Empty);
            }
        }

        public abstract void Compile(string sourceFilePath);
    }
}
