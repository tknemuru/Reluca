using Reluca.Accessors;
using Reluca.Di;
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
            var contexts = CreateMultipleGameContexts(1, 1, ResourceType.In);
            var orgContext = BoardAccessor.DeepCopy(contexts[0]);
            var actual = Target.Evaluate(contexts[0]);
            Console.WriteLine($"評価値1:{actual}");
            Assert.IsTrue(actual > 0);

            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, contexts[0]);
        }
    }
}
