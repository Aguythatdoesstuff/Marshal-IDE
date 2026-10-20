using System;

namespace Compiler
{
    /// <summary>
    /// Configuration object that holds compilation options for a single file,
    /// including AST output settings.
    /// </summary>
    public class CompilationOptions
    {
        /// <summary>
        /// The absolute file path to compile.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// If true, the compiler should output the AST as JSON after compilation.
        /// Standard compilation still occurs.
        /// </summary>
        public bool OutputAst { get; set; }

        /// <summary>
        /// If true, the compiler should ONLY output the AST and skip normal compilation.
        /// The parser runs, but the compilation phase is skipped.
        /// </summary>
        public bool OnlyOutputAst { get; set; }

        /// <summary>
        /// The absolute file path where the AST JSON should be written.
        /// Only relevant if OutputAst or OnlyOutputAst is true.
        /// Includes the filename and extension (e.g., C:\output\file.json).
        /// </summary>
        public string AstOutputPath { get; set; }

        /// <summary>
        /// Returns true if any AST output is requested.
        /// </summary>
        public bool ShouldOutputAst => OutputAst || OnlyOutputAst;

        /// <summary>
        /// Creates a new CompilationOptions with default settings (no AST output).
        /// </summary>
        public static CompilationOptions Default(string filePath)
        {
            return new CompilationOptions
            {
                FilePath = filePath,
                OutputAst = false,
                OnlyOutputAst = false,
                AstOutputPath = null
            };
        }

        /// <summary>
        /// Creates a new CompilationOptions with AST output enabled.
        /// </summary>
        public static CompilationOptions WithAstOutput(string filePath, string astOutputPath, bool onlyAst = false)
        {
            return new CompilationOptions
            {
                FilePath = filePath,
                OutputAst = !onlyAst,
                OnlyOutputAst = onlyAst,
                AstOutputPath = astOutputPath
            };
        }
    }
}
