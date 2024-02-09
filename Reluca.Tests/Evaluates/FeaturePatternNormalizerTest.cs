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
    public class FeaturePatternNormalizerTest : NormalizerUnitTest<FeaturePatternNormalizer>
    {
        [TestMethod]
        public void 特徴パターンを正規化できる()
        {
            // 4桁
            var expected = Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒黒黒黒"));
            var actual = Target.Normalize(FeaturePattern.Type.Diag4, Convert("白白白白"));
            Assert.AreEqual(expected, actual);

            expected = Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒空白白"));
            actual = Target.Normalize(FeaturePattern.Type.Diag4, Convert("白空黒黒"));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, Convert("白白空黒"));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒黒空白"));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, Convert("黒空黒白"));
            Assert.AreNotEqual(expected, actual);

            // 5桁
            expected = Target.Normalize(FeaturePattern.Type.Diag5, Convert("黒空白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Diag5, Convert("白空黒黒黒"));
            Assert.AreEqual(expected, actual);

            // 6桁
            expected = Target.Normalize(FeaturePattern.Type.Diag6, Convert("黒空白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Diag6, Convert("白空黒黒黒黒"));
            Assert.AreEqual(expected, actual);

            // 7桁
            expected = Target.Normalize(FeaturePattern.Type.Diag7, Convert("黒空白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Diag7, Convert("白空黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);

            // 8桁
            expected = Target.Normalize(FeaturePattern.Type.Diag8, Convert("黒空白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Diag8, Convert("白空黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);
            expected = Target.Normalize(FeaturePattern.Type.HorVert2, Convert("黒空白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.HorVert2, Convert("白空黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);
            expected = Target.Normalize(FeaturePattern.Type.HorVert3, Convert("黒空白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.HorVert3, Convert("白空黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);
            expected = Target.Normalize(FeaturePattern.Type.HorVert4, Convert("黒空白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.HorVert4, Convert("白空黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);

            // 9桁
            expected = Target.Normalize(FeaturePattern.Type.Corner3X3, Convert("黒空白白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Corner3X3, Convert("白空黒黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);

            // 10桁
            expected = Target.Normalize(FeaturePattern.Type.Edge2X, Convert("黒空白白白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Edge2X, Convert("白空黒黒黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);
            expected = Target.Normalize(FeaturePattern.Type.Corner2X5, Convert("黒空白白白白白白白白"));
            actual = Target.Normalize(FeaturePattern.Type.Corner2X5, Convert("白空黒黒黒黒黒黒黒黒"));
            Assert.AreEqual(expected, actual);
        }
    }
}
