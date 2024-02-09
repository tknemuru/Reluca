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
            Target.Initialize(resource, new NoneNormalizer());
            var actual = Target.Extract(context);
            Assert.AreEqual(RadixHelper.ToUInt32("2011111021", 3), actual[FeaturePattern.Type.Edge2X][0]);
            Assert.AreEqual(RadixHelper.ToUInt32("2110111202", 3), actual[FeaturePattern.Type.Edge2X][1]);
            Assert.AreEqual(RadixHelper.ToUInt32("2110110112", 3), actual[FeaturePattern.Type.Corner2X5][0]);
        }

        [TestMethod]
        public void 正規化有りで特徴パターンが抽出できる()
        {
            var resource = ReadJsonResource<Dictionary<string, List<List<ulong>>>>(1, 1, ResourceType.In);
            var context = CreateBoardContext(1, 2, ResourceType.In);
            Target.Initialize(resource, new FeaturePatternNormalizer());
            var actual = Target.Extract(context);
            // Assert.AreEqual(RadixHelper.ToUInt32("2011111021", 3), actual[FeaturePattern.Type.Edge2X][0]);
            Assert.AreEqual(RadixHelper.ToUInt32("0211111201", 3), actual[FeaturePattern.Type.Edge2X][0]);
            // Assert.AreEqual(RadixHelper.ToUInt32("2110111202", 3), actual[FeaturePattern.Type.Edge2X][1]);
            Assert.AreEqual(RadixHelper.ToUInt32("0112111020", 3), actual[FeaturePattern.Type.Edge2X][1]);
            // Assert.AreEqual(RadixHelper.ToUInt32("2110110112", 3), actual[FeaturePattern.Type.Corner2X5][0]);
            Assert.AreEqual(RadixHelper.ToUInt32("0112112110", 3), actual[FeaturePattern.Type.Corner2X5][0]);
        }
    }
}
