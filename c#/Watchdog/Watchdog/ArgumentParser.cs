using System;

namespace Watchdog
{
    /// <summary>
    /// Utility class for parsing command-line arguments.
    /// </summary>
    public class ArgumentParser
    {
        private string _metadataPath;

        /// <summary>
        /// Parses command-line arguments to extract the metadata path.
        /// </summary>
        /// <param name="args">Command-line arguments array</param>
        /// <returns>The metadata path if found, or null if not found</returns>
        public string ParseArguments(string[] args)
        {
            _metadataPath = null;

            if (args == null || args.Length == 0)
                return null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("metadataPath=", StringComparison.OrdinalIgnoreCase))
                {
                    _metadataPath = arg.Substring("metadataPath=".Length);
                    return _metadataPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the parsed metadata path.
        /// </summary>
        public string MetadataPath => _metadataPath;

        /// <summary>
        /// Validates if the metadata path argument was provided.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(_metadataPath);
    }
}
