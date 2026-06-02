using System.IO;
using System.Linq;

namespace Compiler.decision
{
    public class DecisionCompiler : BaseCompiler
    {
        public Compiler.DecisionParser.ParsedDecisionFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null || PassedData.Categories == null) return;

            var fileName = PassedData.SourceFileName;
            string indent1 = Ident(1);
            string indent2 = Ident(2);
            foreach (var cat in PassedData.Categories)
            {
                WriteFile("common/decisions/categories", fileName, ".txt", sw =>
                {
                    sw.WriteLine($"{cat.id} = {{");
                    sw.WriteLine($"{indent1}icon = {cat.sprite}");
                    sw.WriteLine($"{indent1}priority = {cat.priority}");
                    sw.WriteLine($"{indent1}allowed = {{");
                    WriteAllowedWithConversions(sw, cat.allowed, rl => rl.depth, rl => rl.trimmedLine);
                    sw.WriteLine($"{indent1}}}");
                    sw.WriteLine($"{indent1}available = {{");
                    WriteAllowedWithConversions(sw, cat.available, rl => rl.depth, rl => rl.trimmedLine);
                    sw.WriteLine($"{indent1}}}");
                    sw.WriteLine($"}}");
                });

                WriteFile("common/decisions", fileName, ".txt", sw =>
                {
                    sw.WriteLine($"{cat.id} = {{");
                });
                
                WriteFile("common/decisions", fileName, ".txt", sw =>
                {
                    foreach (var cat2 in cat.decisions)
                    {
                        sw.WriteLine($"{indent1}{cat2.id} = {{");
                        sw.WriteLine($"{indent2}icon = {cat2.sprite}");
                        sw.WriteLine($"{indent2}cost = {cat2.cost}");
                        sw.WriteLine($"{indent2}allowed = {{");
                        WriteAllowedWithConversions(sw, cat2.allowed, rl => rl.depth, rl => rl.trimmedLine);
                        sw.WriteLine($"{indent2}}}");
                        sw.WriteLine($"{indent2}available = {{");
                        WriteAllowedWithConversions(sw, cat2.available, rl => rl.depth, rl => rl.trimmedLine);
                        sw.WriteLine($"{indent2}}}");
                        sw.WriteLine($"{indent2}complete_effect = {{");
                        WriteAllowedWithConversions(sw, cat2.onClick, rl => rl.depth, rl => rl.trimmedLine);
                        sw.WriteLine($"{indent2}}}");
                        sw.WriteLine($"{indent1}}}");
                    }
                });

                WriteFile("common/decisions", fileName, ".txt", sw =>
                {
                    sw.WriteLine($"}}");
                });
            }
        }
    }
}
