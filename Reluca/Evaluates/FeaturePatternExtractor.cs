using Reluca.Contexts;
using Reluca.Helpers;
using Reluca.Models;
using System.Collections.Generic;
using System.Linq;

namespace Reluca.Evaluates
{
#pragma warning disable CS8618
#pragma warning disable CS8604
#pragma warning disable CS8601
    /// <summary>
    /// 盤面から特徴パターンを抽出する機能を提供します。
    /// </summary>
    public class FeaturePatternExtractor
    {
        /// <summary>
        /// 特徴パターンの位置情報を管理する辞書
        /// </summary>
        private Dictionary<FeaturePattern.Type, List<List<ulong>>> PatternPositions { get; set; }

        /// <summary>
        /// 事前確保した抽出結果格納用の配列。
        /// PatternResults[patternType] = int[] (各サブパターンのインデックス)。
        /// シングルスレッド前提で内部バッファを共有するため、マルチスレッド環境では使用不可。
        /// </summary>
        private readonly Dictionary<FeaturePattern.Type, int[]> _preallocatedResults;

        /// <summary>
        /// コンストラクタ。特徴パターンの位置情報をリソースから読み込み、事前確保用バッファを初期化します。
        /// </summary>
        public FeaturePatternExtractor()
        {
            var resource = FileHelper.ReadJson<Dictionary<string, List<List<ulong>>>>(Properties.Resources.feature_pattern);
            // 文字列操作を避けるために、キーを文字列からenumに変換して保持する
            var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
            PatternPositions = positions;

            // 事前確保
            _preallocatedResults = new Dictionary<FeaturePattern.Type, int[]>();
            foreach (var pattern in PatternPositions)
            {
                _preallocatedResults[pattern.Key] = new int[pattern.Value.Count];
            }
        }

        /// <summary>
        /// 初期化を行います。
        /// </summary>
        /// <param name="resource">特徴パターンの位置情報辞書</param>
        public void Initialize(Dictionary<string, List<List<ulong>>>? resource)
        {
            // 文字列操作を避けるために、キーを文字列からenumに変換して保持する
            var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
            PatternPositions = positions;
        }

        /// <summary>
        /// 特徴パターンを抽出します。
        /// 毎回新しい Dictionary と List を生成するため、アロケーションが発生します。
        /// 探索エンジンからの呼び出しには ExtractNoAlloc の使用を推奨します。
        /// </summary>
        /// <param name="context">盤状態</param>
        /// <returns>特徴パターンの辞書</returns>
        public Dictionary<FeaturePattern.Type, List<int>> Extract(BoardContext context)
        {
            var result = new Dictionary<FeaturePattern.Type, List<int>>();
            foreach (var pattern in PatternPositions)
            {
                result[pattern.Key] = new List<int>();
                foreach (var positions in pattern.Value)
                {
                    result[pattern.Key].Add(ConvertToTernaryIndex(context, positions));
                }
            }
            return result;
        }

        /// <summary>
        /// 特徴パターンを抽出します（アロケーションなし版）。
        /// 戻り値は内部バッファへの参照であり、次回呼び出しで上書きされます。
        /// シングルスレッド前提のメソッドです。マルチスレッド環境ではデータ競合が発生するため、
        /// ThreadLocal バッファまたはスレッドごとの独立したインスタンスを使用してください。
        /// </summary>
        /// <param name="context">盤状態</param>
        /// <returns>特徴パターンの辞書（内部バッファへの参照）</returns>
        public Dictionary<FeaturePattern.Type, int[]> ExtractNoAlloc(BoardContext context)
        {
            foreach (var pattern in PatternPositions)
            {
                var arr = _preallocatedResults[pattern.Key];
                for (int i = 0; i < pattern.Value.Count; i++)
                {
                    arr[i] = ConvertToTernaryIndex(context, pattern.Value[i]);
                }
            }
            return _preallocatedResults;
        }

        /// <summary>
        /// 盤状態を特徴パターンに従って3進数変換したインデックスに変換します。
        /// </summary>
        /// <param name="context">盤状態</param>
        /// <param name="positions">特徴パターンの位置情報</param>
        /// <returns>盤状態を特徴パターンに従って3進数変換したインデックス</returns>
        private static int ConvertToTernaryIndex(BoardContext context, List<ulong> positions)
        {
            int index = 0;
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
