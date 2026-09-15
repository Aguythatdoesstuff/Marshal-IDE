using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{
    public class EquipmentParser : BaseParser
       {
           public EquipmentParser()
           {
               Compiler.Logging.Logger.LogComponent("Parser", "EquipmentParser initialized.");
           }
            // store the most recently parsed file for other components to use
            public ParsedEventFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            // Use ParsedEventFile to collect all events from this file and store the file name
            var parsedFile = new ParsedEventFile { SourceFileName = fileName };





            // save parsed result
            LastParsedFile = parsedFile;
        }
    }
}
