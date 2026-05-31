using System.Text.RegularExpressions;

namespace Compiler
{
    public class EventValidator : BaseValidator
    {
        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            
            return false;
        }
    }
}

