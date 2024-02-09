using Reluca.Evaluates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Evaluates
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    public class FeaturePatternEvaluatorTest : BaseUnitTest<FeaturePatternEvaluator>
    {
        [TestMethod]
        public void ゲーム状態の評価が行える()
        {
            var context = CreateGameContext(1, 1, ResourceType.In);
            var actual = Target.Evaluate(context);
            Assert.IsTrue(actual > 0);
        }
    }
}
