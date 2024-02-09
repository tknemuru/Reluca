using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 特徴パターンの正規化機能を提供します。
    /// </summary>
    public class FeaturePatternNormalizer : INormalizable
    {
        /// <summary>
        /// キーと桁数を分離する文字
        /// </summary>
        private const char KeyDigitSeparator = '$';

        /// <summary>
        /// 正規化辞書
        /// </summary>
        private Dictionary<ushort, Dictionary<ushort, ushort>> NormalizeDic {  get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FeaturePatternNormalizer()
        {
            NormalizeDic = ReadNoramlizeDic();
        }

        /// <summary>
        /// 特徴パターンを正規化します。
        /// </summary>
        /// <param name="type">特徴パターンの種類</param>
        /// <param name="org">正規化前の値</param>
        /// <returns>正規化した特徴パターン</returns>
        public ushort Normalize(FeaturePattern.Type type, ushort org)
        {
            var digit = FeaturePattern.GetDigit(type);
            Debug.Assert(NormalizeDic.ContainsKey(digit), "辞書に情報が存在しません。");
            Debug.Assert(NormalizeDic[digit].ContainsKey(org), "辞書に情報が存在しません。");
            return NormalizeDic[digit][org];
        }

        /// <summary>
        /// 正規化辞書を読み込みます。
        /// </summary>
        /// <returns>正規化辞書</returns>
        private static Dictionary<ushort, Dictionary<ushort, ushort>> ReadNoramlizeDic()
        {
            var result = new Dictionary<ushort, Dictionary<ushort, ushort>>();
            var csv = Properties.Resources.feature_normalize;
            var keyValues = csv.Split(',');
            var length = keyValues.Length;
            Debug.Assert((length % 2 == 0), "要素数が奇数です。");

            // 取り得る桁数で初期化
            for(ushort i = FeaturePattern.TypeDigit.Min; i <= FeaturePattern.TypeDigit.Max; i++)
            {
                result[i] = new Dictionary<ushort, ushort>();
            }

            // csvを変換していく
            for (int i = 0; i < length; i += 2)
            {
                var orgDigit = keyValues[i].Split(KeyDigitSeparator);
                Debug.Assert(orgDigit.Length == 2, "要素数が不正です。");
                var org = ushort.Parse(orgDigit[0]);
                var digit = ushort.Parse(orgDigit[1]);
                var dest = ushort.Parse(keyValues[i + 1]);
                result[digit][org] = dest;
            }
            return result;
        }
    }
}
