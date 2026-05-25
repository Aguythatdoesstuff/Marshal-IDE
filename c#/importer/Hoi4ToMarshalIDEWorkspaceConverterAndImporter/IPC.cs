public static class IPC
{
    public static void Send(string type, object data)
    {
        var message = new
        {
            type = type,
            payload = data,
            timestamp = DateTime.UtcNow
        };
        Console.WriteLine($"[[IPC]]:{System.Text.Json.JsonSerializer.Serialize(message)}");
    }

    public static void FatalError(string msg) => Send("FATAL", new { message = msg });
}