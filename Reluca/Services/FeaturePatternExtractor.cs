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
    public class FeaturePatternExtractor : IServiceable<GameContext, Dictionary<string, List<List<ulong>>>>
    {
        public Dictionary<string, List<List<ulong>>> Execute(GameContext input)
        {
            var patterns = FileHelper.ReadJson<Dictionary<string, List<List<ulong>>>>(Properties.Resources.feature_pattern);
            Console.WriteLine(patterns);
            return patterns;
        }
    }
}
