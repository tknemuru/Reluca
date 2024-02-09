using Reluca.Evaluates;
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
    public class EvaluatedValueSignNoramalizerTest : NormalizerUnitTest<EvaluatedValueSignNoramalizer>
    {
        [TestMethod]
        public void 評価値の符号を正規化できる()
        {
            Assert.AreEqual(-1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒黒黒黒")));
            Assert.AreEqual(1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("白白白白")));

            Assert.AreEqual(1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒空白白")));
            Assert.AreEqual(-1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("白空黒黒")));
            Assert.AreEqual(1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("白白空黒")));
            Assert.AreEqual(-1, Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒黒空白")));
        }
    }
}
