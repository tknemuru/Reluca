using Reluca.Contexts;
using Reluca.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Services
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    public class FeaturePatternExtractor : IServiceable<GameContext, Dictionary<string, List<List<ushort>>>>
    {
        public Dictionary<string, List<List<ushort>>> Execute(GameContext input)
        {
            var rootDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var patterns = FileHelper.ReadJson<Dictionary<string, List<List<ushort>>>>("C:\\dev\\tknemuru\\Reluca\\Reluca\\Resources\\feature-patterns.json");
            Console.WriteLine(patterns);
            return patterns;
        }
    }
}
