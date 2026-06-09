using System;
using System.IO;

namespace Compiler
{
    public class DdsCompiler : BaseCompiler
    {
        public override void Compile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SourceAbsolutePath) || !File.Exists(SourceAbsolutePath))
                {
                    Compiler.Logging.Logger.LogComponent("DdsCompiler", "SourceAbsolutePath missing or file not found.");
                    return;
                }

                // Ensure output folders
                var relativeImageTarget = Path.Combine("gfx", "interface", "handledByMarshalIDE");
                var relativeGfxTarget = Path.Combine("interface", "handledByMarshalIDE");

                var imageOutDir = Path.Combine(OutputPath ?? string.Empty, relativeImageTarget);
                var gfxOutDir = Path.Combine(OutputPath ?? string.Empty, relativeGfxTarget);

                Directory.CreateDirectory(imageOutDir);
                Directory.CreateDirectory(gfxOutDir);

                var sourceFileName = Path.GetFileName(SourceAbsolutePath);
                var nameOnly = Path.GetFileNameWithoutExtension(sourceFileName);

                // Copy the DDS image into the gfx/interface/handledByMarshalIDE folder
                var destImagePath = Path.Combine(imageOutDir, sourceFileName);
                File.Copy(SourceAbsolutePath, destImagePath, true);

                // Create the .gfx definition file in interface/handledByMarshalIDE
                var gfxFileName = nameOnly + ".gfx";
                var gfxFilePath = Path.Combine(gfxOutDir, gfxFileName);

                using (var sw = new StreamWriter(new FileStream(gfxFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    sw.WriteLine("spriteTypes = {");
                    sw.WriteLine("    spriteType = {");
                    sw.WriteLine($"        name = \"GFX_{nameOnly}\"");
                    sw.WriteLine($"        texturefile = \"gfx/interface/handledByMarshalIDE/{sourceFileName}\"");
                    sw.WriteLine("    }");
                    sw.WriteLine("}");
                }

                Compiler.Logging.Logger.LogComponent("DdsCompiler", $"Processed {sourceFileName}");
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogComponent("DdsCompiler", "Failed: " + ex.Message);
                Compiler.Logging.Logger.ReportUnhandledException(ex);
                IPC.Send("FatalError", ex.Message);
                throw;
            }
        }
    }
}
