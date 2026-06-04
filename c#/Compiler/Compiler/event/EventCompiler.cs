using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;

namespace Compiler.@event
{
    public class EventCompiler : BaseCompiler
    {
        public EventCompiler()
        {
            Compiler.Logging.Logger.LogComponent("Compiler", "EventCompiler initialized.");
        }
        // Parsed events provided by the parser
        public ParsedEventFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null || PassedData.Events == null) return;
            string lastNamespace = null;
            string indent1 = Ident(1);
            string indent2 = Ident(2);

            foreach (var ev in PassedData.Events)
            {
                var fileName = PassedData.SourceFileName;

                WriteFile("events", fileName, ".txt", (sw, created) =>
                {
                    // Dynamically extract the namespace by cutting off the '.' and the id number (e.g., "china_event.1" -> "china_event")
                    string ns = ev.id;
                    int dotIndex = ev.id.LastIndexOf('.');
                    if (dotIndex > 0)
                    {
                        ns = ev.id.Substring(0, dotIndex);
                    }

                    // Only print the namespace header when it actually changes
                    if (lastNamespace != ns)
                    {
                        sw.WriteLine($"add_namespace = {ns}\n");
                    }

                    sw.WriteLine($"{ev.type.ToString().ToLowerInvariant()}_event = {{");
                    sw.WriteLine($"{indent1}id = {ev.id}");
                    sw.WriteLine($"{indent1}is_triggered_only = yes");
                    sw.WriteLine($"{indent1}title = {ev.id}_title");
                    sw.WriteLine($"{indent1}desc = {ev.id}_desc");
                    if (ev.type == EventType.News) sw.WriteLine($"{indent1}major = yes");
                    sw.WriteLine($"{indent1}picture = \"{ev.sprite}\"");

                    foreach (var line in ev.rawLine)
                    {
                        WriteAllowedWithConversions(sw, ev.rawLine, l => l.depth, l => l.trimmedLine);
                        break; // helper processed the entire collection
                    }

                    int optIndex = 1;
                    foreach (var option in ev.option)
                    {
                        sw.WriteLine($"{indent1}option = {{");
                        sw.WriteLine($"{indent2}name = {ev.id}_option_{optIndex}");
                        if (option.rawLine != null)
                        {
                            WriteAllowedWithConversions(sw, option.rawLine, l => l.depth, l => l.trimmedLine);
                        }
                        sw.WriteLine($"{indent1}}}");
                        optIndex++;
                    }

                    sw.WriteLine($"}}\n");
                    lastNamespace = ns;
                });

                WriteFile("localisation/english/events", fileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");

                    // Match Paradox formatting precisely using the ':0 ' notation rule
                    sw.WriteLine($" {ev.id}_title:0 \"{ev.name}\"");
                    sw.WriteLine($" {ev.id}_desc:0 \"{ev.desc}\"");

                    int optIndex = 1;
                    foreach (var option in ev.option)
                    {
                        sw.WriteLine($" {ev.id}_option_{optIndex}:0 \"{option.name}\"");
                        optIndex++;
                    }
                });
            }
        }
    }
}