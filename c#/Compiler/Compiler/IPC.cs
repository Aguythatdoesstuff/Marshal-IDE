namespace Compiler
{
    internal static class IPC
    {
        internal static void Send(string type, object data)
        {
            var message = new
            {
                type = type,
                payload = data,
                timestamp = DateTime.UtcNow
            };
            Console.WriteLine($"[[IPC]]:{System.Text.Json.JsonSerializer.Serialize(message)}");
        }

        internal static void Log(string type, string data)
        {
            Console.WriteLine($"[[IPC]] - [[{type}]]: {data}");
        }

        internal static void FatalError(string msg) => Send("FATAL", new { message = msg });
    }
}
