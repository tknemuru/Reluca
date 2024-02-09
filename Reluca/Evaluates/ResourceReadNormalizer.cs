using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 正規化機能を提供します。
    /// </summary>
    public abstract class ResourceReadNormalizer : INormalizable
    {
        /// <summary>
        /// キーと桁数を分離する文字
        /// </summary>
        private const char KeyDigitSeparator = '$';

        /// <summary>
        /// 正規化辞書
        /// </summary>
        private Dictionary<uint, Dictionary<int, int>> NormalizeDic { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ResourceReadNormalizer()
        {
            NormalizeDic = ReadNoramlizeDic();
        }

        /// <summary>
        /// 正規化します。
        /// </summary>
        /// <param name="type">特徴パターンの種類</param>
        /// <param name="org">正規化前の値</param>
        /// <returns>正規化した値</returns>
        public int Normalize(FeaturePattern.Type type, int org)
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
        private Dictionary<uint, Dictionary<int, int>> ReadNoramlizeDic()
        {
            var result = new Dictionary<uint, Dictionary<int, int>>();
            var csv = GetResource();
            var keyValues = csv.Split(',');
            var length = keyValues.Length;
            Debug.Assert((length % 2 == 0), "要素数が奇数です。");

            // 取り得る桁数で初期化
            for (uint i = FeaturePattern.TypeDigit.Min; i <= FeaturePattern.TypeDigit.Max; i++)
            {
                result[i] = new Dictionary<int, int>();
            }

            // csvを変換していく
            for (int i = 0; i < length; i += 2)
            {
                var orgDigit = keyValues[i].Split(KeyDigitSeparator);
                Debug.Assert(orgDigit.Length == 2, "要素数が不正です。");
                var org = int.Parse(orgDigit[0]);
                var digit = uint.Parse(orgDigit[1]);
                var dest = int.Parse(keyValues[i + 1]);
                result[digit][org] = dest;
            }
            return result;
        }

        /// <summary>
        /// リソースを取得します。
        /// </summary>
        /// <returns>リソース文字列</returns>
        protected abstract string GetResource();
    }
}
