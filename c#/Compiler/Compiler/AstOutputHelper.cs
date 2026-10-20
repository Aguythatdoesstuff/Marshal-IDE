using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Compiler
{
    /// <summary>
    /// Helper class for serializing parsed AST structures to JSON and writing them to disk.
    /// </summary>
    public static class AstOutputHelper
    {
        /// <summary>
        /// Serializes an AST object to JSON and writes it to the specified file path.
        /// </summary>
        /// <param name="astObject">The AST object to serialize (typically a ParsedXxxFile class)</param>
        /// <param name="outputPath">Absolute file path where the JSON should be written</param>
        /// <returns>True if successful; false otherwise</returns>
        public static bool WriteAstToJson(object astObject, string outputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    Compiler.Logging.Logger.LogMain("[AstOutputHelper] Output path is null or empty.");
                    return false;
                }

                if (astObject == null)
                {
                    Compiler.Logging.Logger.LogMain("[AstOutputHelper] AST object is null; cannot serialize.");
                    return false;
                }

                // Ensure directory exists
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Compiler.Logging.Logger.LogMain($"[AstOutputHelper] Failed to create directory '{directory}': {ex.Message}");
                        return false;
                    }
                }

                // Serialize the AST to JSON with indented formatting
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(astObject, options);

                // Write to file
                File.WriteAllText(outputPath, json);
                Compiler.Logging.Logger.LogMain($"[AstOutputHelper] AST successfully written to: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogMain($"[AstOutputHelper] Error writing AST to JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts the parsed AST from a validator's parser instance.
        /// Different file types have different parser return types; this handles them uniformly.
        /// </summary>
        /// <param name="validator">The validator instance that ran during file validation</param>
        /// <returns>The parsed AST object, or null if no parser ran or no parsed data exists</returns>
        public static object GetAstFromValidator(BaseValidator validator)
        {
            if (validator == null) return null;

            try
            {
                var parser = validator.GetParserInstance();
                if (parser == null) return null;

                // Use reflection to safely access the LastParsedFile property if it exists
                var prop = parser.GetType().GetProperty("LastParsedFile", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (prop != null)
                {
                    return prop.GetValue(parser);
                }

                return null;
            }
            catch (Exception ex)
            {
                Compiler.Logging.Logger.LogMain($"[AstOutputHelper] Error extracting AST from validator: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a structured wrapper around the AST for consistent output format.
        /// </summary>
        /// <param name="ast">The AST object to wrap</param>
        /// <param name="sourceFile">The original file that was parsed</param>
        /// <param name="parserErrors">Any parser errors that occurred</param>
        /// <returns>A structured output object suitable for JSON serialization</returns>
        public static object WrapAstOutput(object ast, string sourceFile, IEnumerable<ParsingError> parserErrors = null)
        {
            var parserErrorList = new List<object>();
            if (parserErrors != null)
            {
                foreach (var err in parserErrors)
                {
                    parserErrorList.Add(new
                    {
                        line = err.LineNumber,
                        message = err.ErrorMessage
                    });
                }
            }

            return new
            {
                sourceFile = sourceFile,
                timestamp = DateTime.UtcNow.ToString("o"),
                ast = ast,
                parserErrors = parserErrorList,
                parserErrorCount = parserErrorList.Count
            };
        }
    }
}
