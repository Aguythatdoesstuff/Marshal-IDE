using System;
using System.IO;

namespace Compiler.@event
{
    public class EventCompiler : BaseCompiler
    {
        public override void Compile(string sourceFilePath)
        {
            // Basic compilation: read original file and emit it into events subfolder as filename.txt
            string contents;
            try
            {
                contents = File.ReadAllText(sourceFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read source for compilation: " + ex.Message, ex);
            }

            string finalName = null;
            if (!string.IsNullOrWhiteSpace(this.SourceFileName))
            {
                finalName = Path.GetFileNameWithoutExtension(this.SourceFileName);
            }
            else
            {
                finalName = Path.GetFileNameWithoutExtension(sourceFilePath);
            }

            WriteFile("events", finalName, ".txt", contents);
        }
    }
}
