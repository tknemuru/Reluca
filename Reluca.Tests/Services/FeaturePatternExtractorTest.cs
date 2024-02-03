using Reluca.Contexts;
using Reluca.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Services
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    public class FeaturePatternExtractorTest : BaseUnitTest<FeaturePatternExtractor>
    {
        [TestMethod]
        public void 特徴パターンが抽出できる()
        {
            Target.Execute(new GameContext());
        }
    }
}
