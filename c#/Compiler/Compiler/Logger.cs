using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Compiler.Logging
{
    internal static class Logger
    {
        private static readonly object _initLock = new object();
        private static bool _initialized = false;

        private static string _sessionPath = null!;
        private static string _mainIndexPath = null!;
        private static readonly object _writeLock = new object();

        // Chunk tracking configuration
        private static int _currentChunkIndex = 1;
        private static int _currentChunkLineCount = 0;
        private static StreamWriter? _currentChunkWriter = null;
        private const int MaxLinesPerChunk = 5000;

        public static void Initialize(string rootLogPath)
        {
            lock (_initLock)
            {
                if (_initialized) return;

                if (string.IsNullOrWhiteSpace(rootLogPath))
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    rootLogPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "logs");
                }

                try
                {
                    if (!Path.IsPathRooted(rootLogPath)) rootLogPath = Path.GetFullPath(rootLogPath);
                }
                catch
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    rootLogPath = Path.Combine(desktop, "marshal-ide-compiler-debbug", "logs");
                }

                // Create unique session folder based on timestamp
                string sessionId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
                _sessionPath = Path.Combine(rootLogPath, $"session_{sessionId}");
                Directory.CreateDirectory(_sessionPath);

                // Setup the main index markdown file
                _mainIndexPath = Path.Combine(_sessionPath, "compiler-main.md");

                // Set up markdown header
                File.WriteAllText(_mainIndexPath, $"# Compiler Run Index ({DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC)\n\n", new UTF8Encoding(false));

                // Global unhandled exception handlers
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try
                    {
                        var ex = e.ExceptionObject as Exception;
                        WriteCrashReport(ex ?? new Exception("Unhandled exception without Exception object"));
                    }
                    catch { }
                };

                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try
                    {
                        WriteCrashReport(e.Exception ?? new Exception("UnobservedTaskException"));
                    }
                    catch { }
                };

                _initialized = true;
                LogMain($"Logger initialized. Session: {sessionId}");
            }
        }

        public static void LogMain(string message)
        {
            EnsureInit();
            WriteToActiveChunk("MAIN", message);
        }

        public static void LogComponent(string component, string message)
        {
            EnsureInit();
            if (string.IsNullOrWhiteSpace(component)) component = "COMPONENT";

            // All component logs now redirect straight into the shared, unified chunk files
            WriteToActiveChunk(component.ToUpperInvariant(), message);
        }

        public static void LogWarning(string component, string message)
        {
            EnsureInit();
            if (string.IsNullOrWhiteSpace(component)) component = "COMPONENT";
            // Write warnings under a WARN-<COMPONENT> prefix so they are easy to locate
            WriteToActiveChunk($"WARN-{component.ToUpperInvariant()}", message);
        }

        private static void WriteToActiveChunk(string prefix, string message)
        {
            var ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var cleanMessage = message.Replace("\r", "").Replace("\n", " ");
            var formattedLine = $"[{ts}] [{prefix}] {cleanMessage}";

            lock (_writeLock)
            {
                EnsureChunkOpen();

                _currentChunkWriter!.WriteLine(formattedLine);
                _currentChunkLineCount++;

                if (_currentChunkLineCount >= MaxLinesPerChunk)
                {
                    CloseCurrentChunk();
                }
            }
        }

        private static void EnsureChunkOpen()
        {
            if (_currentChunkWriter != null) return;

            string chunkFileName = $"logs_{_currentChunkIndex}.md";
            string chunkPath = Path.Combine(_sessionPath, chunkFileName);

            var fs = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _currentChunkWriter = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
            _currentChunkLineCount = 0;

            // Appends a clickable relative Markdown link pointing directly to the log file chunk
            using var indexFs = new FileStream(_mainIndexPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var indexSw = new StreamWriter(indexFs, new UTF8Encoding(false));
            indexSw.WriteLine($"- [{chunkFileName}](./{chunkFileName})");
        }

        private static void CloseCurrentChunk()
        {
            if (_currentChunkWriter == null) return;

            _currentChunkWriter.Flush();
            _currentChunkWriter.Dispose();
            _currentChunkWriter = null;

            _currentChunkIndex++;
            _currentChunkLineCount = 0;
        }

        private static void WriteCrashReport(Exception ex)
        {
            try
            {
                EnsureInit();

                lock (_writeLock)
                {
                    if (_currentChunkWriter != null)
                    {
                        _currentChunkWriter.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [CRASH DETECTED]");
                        CloseCurrentChunk();
                    }
                }

                var path = Path.Combine(_sessionPath, "crash_report.md");
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(false));

                sw.WriteLine($"Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                WriteException(sw, ex);

                // Add link to crash report in index
                using var indexFs = new FileStream(_mainIndexPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var indexSw = new StreamWriter(indexFs, new UTF8Encoding(false));
                indexSw.WriteLine($"- [**CRASH REPORT**](./crash_report.md)");
            }
            catch { }
        }

        // Public wrapper to allow external callers to report unhandled exceptions
        public static void ReportUnhandledException(Exception ex)
        {
            try
            {
                WriteCrashReport(ex ?? new Exception("Unhandled exception without Exception object"));
            }
            catch { }
        }

        private static void WriteException(StreamWriter sw, Exception ex)
        {
            if (sw == null || ex == null) return;

            sw.WriteLine($"Exception: {ex.Message}");
            sw.WriteLine($"Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                sw.WriteLine("--- Inner Exception ---");
                WriteException(sw, ex.InnerException);
            }
        }

        private static void EnsureInit()
        {
            if (!_initialized) throw new InvalidOperationException("Logger not initialized. Call Logger.Initialize(path) before use.");
        }
    }
}