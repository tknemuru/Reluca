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
    /// <summary>
    /// FeaturePatternEvaluator の単体テストクラスです。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FeaturePatternEvaluatorTest : BaseUnitTest<FeaturePatternEvaluator>
    {
        [TestMethod]
        public void ゲーム状態の評価が行える()
        {
            var contexts = CreateMultipleGameContexts(1, 1, ResourceType.In);
            var orgContext = BoardAccessor.DeepCopy(contexts[0]);
            var actual = Target.Evaluate(contexts[0]);
            Assert.AreEqual(-2685683177938380, actual);

            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, contexts[0]);

            orgContext = BoardAccessor.DeepCopy(contexts[1]);
            actual = Target.Evaluate(contexts[1]);
            Assert.AreEqual(-8595967991902324, actual);

            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, contexts[1]);
        }
    }
}
