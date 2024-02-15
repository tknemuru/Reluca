using Reluca.Contexts;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Evaluates
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    public class FeaturePatternExtractorTest : BaseUnitTest<FeaturePatternExtractor>
    {
        [TestMethod]
        public void 正規化無しで特徴パターンが抽出できる()
        {
            var resource = ReadJsonResource<Dictionary<string, List<List<ulong>>>>(1, 1, ResourceType.In);
            var context = CreateBoardContext(1, 2, ResourceType.In);
            var target = new FeaturePatternExtractor();
            target.Initialize(resource);
            var actual = target.Extract(context);
            Assert.AreEqual(RadixHelper.ToInt32("2011111021", 3), actual[FeaturePattern.Type.Edge2X][0]);
            Assert.AreEqual(RadixHelper.ToInt32("2110111202", 3), actual[FeaturePattern.Type.Edge2X][1]);
            Assert.AreEqual(RadixHelper.ToInt32("2110110112", 3), actual[FeaturePattern.Type.Corner2X5][0]);
        }
    }
}
