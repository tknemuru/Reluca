using Reluca.Contexts;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Models;
using Reluca.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
#pragma warning disable CS8618
#pragma warning disable CS8604
#pragma warning disable CS8601
    public class FeaturePatternExtractor
    {
        /// <summary>
        /// 特徴パターンの位置情報を管理する辞書
        /// </summary>
        private Dictionary<FeaturePattern.Type, List<List<ulong>>> PatternPositions { get; set; }

        /// <summary>
        /// 正規化機能
        /// </summary>
        private INormalizable Normalizer { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FeaturePatternExtractor()
        {
            PatternPositions = new Dictionary<FeaturePattern.Type, List<List<ulong>>>();
        }

        /// <summary>
        /// 特徴パターンの位置情報辞書を読み込みます。
        /// </summary>
        public void Initialize()
        {
            var resource = FileHelper.ReadJson<Dictionary<string, List<List<ulong>>>>(Properties.Resources.feature_pattern);
            // 文字列操作を避けるために、キーを文字列からenumに変換して保持する
            var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
            PatternPositions = positions;

            Normalizer = DiProvider.Get().GetService<FeaturePatternNormalizer>();
        }

        /// <summary>
        /// 特徴パターンの位置情報辞書を読み込みます。
        /// </summary>
        /// <param name="resource">特徴パターンの位置情報辞書</param>
        public void Initialize(Dictionary<string, List<List<ulong>>>? resource, INormalizable normalizer)
        {
            // 文字列操作を避けるために、キーを文字列からenumに変換して保持する
            var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
            PatternPositions = positions;

            Normalizer = normalizer;
        }

        /// <summary>
        /// 特徴パターンを抽出します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>特徴パターン</returns>
        public Dictionary<FeaturePattern.Type, List<uint>> Extract(BoardContext context)
        {
            var result = new Dictionary<FeaturePattern.Type, List<uint>>();
            foreach (var pattern in PatternPositions)
            {
                result[pattern.Key] = new List<uint>();
                foreach (var positions in pattern.Value)
                {
                    result[pattern.Key].Add(Normalizer.Normalize(pattern.Key, ConvertToTernaryIndex(context, positions)));
                }
            }
            return result;
        }

        /// <summary>
        /// 盤状態を特徴パターンに従って3進数変換したインデックスに変換します。
        /// </summary>
        /// <param name="context">盤状態</param>
        /// <param name="positions">特徴パターンの位置情報</param>
        /// <returns>盤状態を特徴パターンに従って3進数変換したインデックス</returns>
        private static uint ConvertToTernaryIndex(BoardContext context, List<ulong> positions)
        {
            uint index = 0;
            var length = positions.Count;
            for (var i = 0; i < length; i++)
            {
                var value = FeaturePattern.BoardStateSequence.Empty;
                if ((context.White & positions[i]) > 0)
                {
                    value = FeaturePattern.BoardStateSequence.White;
                }
                else if ((context.Black & positions[i]) > 0)
                {
                    value = FeaturePattern.BoardStateSequence.Black;
                }
                index += value;
                if (i < length - 1)
                {
                    index *= 3;
                }
            }
            return index;
        }
    }
}
