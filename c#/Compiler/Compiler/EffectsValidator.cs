using System;
using System.Collections.Generic;
using System.IO;

namespace Compiler
{
    // EffectsValidator handles .scriptedeffect files and enforces that when a
    // block named "scripted effect" is encountered, the following lines are
    // indented one level deeper (or opened with a brace).
    public class EffectsValidator : BaseFileValidator
    {
        public EffectsValidator()
        {
            // The keyword that indicates an expected block increase
            ExpectedIndentationBlocks.Add("scripted effect");
        }

        // Inherits BaseFileValidator behavior; override if more specialized parsing is needed
    }
}
