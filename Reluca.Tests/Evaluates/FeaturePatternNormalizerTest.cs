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
    public class FeaturePatternNormalizerTest : BaseUnitTest<FeaturePatternNormalizer>
    {
        /// <summary>
        /// 盤状態と3進数の変換辞書
        /// </summary>
        private static readonly Dictionary<char, ushort> StateDic = new Dictionary<char, ushort>()
        {
            ['白'] = FeaturePattern.BoardStateSequence.White,
            ['空'] = FeaturePattern.BoardStateSequence.Empty,
            ['黒'] = FeaturePattern.BoardStateSequence.Black
        };

        [TestMethod]
        public void 特徴パターンを正規化できる()
        {
            var expected = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("黒黒黒黒"), 3));
            var actual = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("白白白白"), 3));
            Assert.AreEqual(expected, actual);

            expected = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("黒空白白"), 3));
            actual = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("白空黒黒"), 3));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("白白空黒"), 3));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("黒黒空白"), 3));
            Assert.AreEqual(expected, actual);
            actual = Target.Normalize(FeaturePattern.Type.Diag4, RadixHelper.ToUInt16(Convert("黒空黒白"), 3));
            Assert.AreNotEqual(expected, actual);
        }

        /// <summary>
        /// 文字列の盤状態を3進数の文字列に変換します。
        /// </summary>
        /// <param name="state">文字列の盤状態</param>
        /// <returns>3進数の文字列</returns>
        private string Convert(string state)
        {
            return string.Join(string.Empty, state.Select(s => StateDic[s].ToString()));
        }
    }
}
