using Reluca.Contexts;
using Reluca.Helpers;
using Reluca.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Reluca.Evaluates
{
#pragma warning disable CS8618
#pragma warning disable CS8604
#pragma warning disable CS8601
    /// <summary>
    /// 盤面から特徴パターンを抽出する機能を提供します。
    /// 差分更新用のマス→パターン逆引きテーブルも構築します。
    /// </summary>
    public class FeaturePatternExtractor
    {
        /// <summary>
        /// 盤面のマス数（8x8）
        /// </summary>
        private const int BoardSize = 64;

        /// <summary>
        /// マスからパターンへの逆引き情報です。
        /// あるマスが変化した時に影響を受けるパターンとその差分計算に必要な情報を保持します。
        /// </summary>
        public readonly struct PatternMapping
        {
            /// <summary>
            /// パターンの種類
            /// </summary>
            public readonly FeaturePattern.Type PatternType;

            /// <summary>
            /// パターン内のサブパターンインデックス
            /// </summary>
            public readonly int SubPatternIndex;

            /// <summary>
            /// このマスの 3 進数における重み（3^i）。
            /// パターン定義の先頭マス（i=0）が最上位桁となるため、
            /// i 番目のマスの重みは 3^(length-1-i) です。
            /// </summary>
            public readonly int TernaryWeight;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="patternType">パターンの種類</param>
            /// <param name="subPatternIndex">サブパターンインデックス</param>
            /// <param name="ternaryWeight">3 進数における重み</param>
            public PatternMapping(FeaturePattern.Type patternType, int subPatternIndex, int ternaryWeight)
            {
                PatternType = patternType;
                SubPatternIndex = subPatternIndex;
                TernaryWeight = ternaryWeight;
            }
        }

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
        /// マス → パターン逆引きテーブル。
        /// _squareToPatterns[square] は、そのマスを含む全パターンの情報を保持します。
        /// 差分更新時に、変化したマスから影響を受けるパターンを O(1) で特定するために使用します。
        /// </summary>
        private PatternMapping[][] _squareToPatterns;

        /// <summary>
        /// コンストラクタ。特徴パターンの位置情報をリソースから読み込み、事前確保用バッファと逆引きテーブルを初期化します。
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

            // 逆引きテーブルの構築
            BuildSquareToPatterns();
        }

        /// <summary>
        /// 初期化を行います。
        /// PatternPositions の更新に合わせて _preallocatedResults と逆引きテーブルも再構築し、
        /// ExtractNoAlloc および差分更新が正しく動作する状態を維持します。
        /// </summary>
        /// <param name="resource">特徴パターンの位置情報辞書</param>
        public void Initialize(Dictionary<string, List<List<ulong>>>? resource)
        {
            // 文字列操作を避けるために、キーを文字列からenumに変換して保持する
            var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
            PatternPositions = positions;

            // _preallocatedResults を PatternPositions に基づいて再構築する
            _preallocatedResults.Clear();
            foreach (var pattern in PatternPositions)
            {
                _preallocatedResults[pattern.Key] = new int[pattern.Value.Count];
            }

            // 逆引きテーブルも再構築する
            BuildSquareToPatterns();
        }

        /// <summary>
        /// マス → パターン逆引きテーブルを構築します。
        /// PatternPositions の全パターン・全サブパターン・全マス位置を走査し、
        /// 各マス（0-63）に対してそのマスを含むパターン情報を収集します。
        /// </summary>
        private void BuildSquareToPatterns()
        {
            // 各マスに対するパターン情報を一時的にリストで構築
            var tempLists = new List<PatternMapping>[BoardSize];
            for (int i = 0; i < BoardSize; i++)
            {
                tempLists[i] = new List<PatternMapping>();
            }

            foreach (var pattern in PatternPositions)
            {
                var patternType = pattern.Key;
                var subPatterns = pattern.Value;

                for (int subIndex = 0; subIndex < subPatterns.Count; subIndex++)
                {
                    var positions = subPatterns[subIndex];
                    int length = positions.Count;

                    for (int posIndex = 0; posIndex < length; posIndex++)
                    {
                        ulong mask = positions[posIndex];
                        int square = BitOperations.TrailingZeroCount(mask);
                        Debug.Assert(square >= 0 && square < BoardSize,
                            $"パターン {patternType} サブパターン {subIndex} 位置 {posIndex} のマス {square} が範囲外です");

                        // 重み = 3^(length-1-posIndex)
                        // ConvertToTernaryIndex は先頭マスが最上位桁なので、
                        // 先頭(posIndex=0)の重みが最大、末尾(posIndex=length-1)の重みが 3^0=1
                        int weight = 1;
                        for (int w = 0; w < length - 1 - posIndex; w++)
                        {
                            weight *= 3;
                        }

                        tempLists[square].Add(new PatternMapping(patternType, subIndex, weight));
                    }
                }
            }

            // リストを配列に変換（探索中のインデクサアクセス高速化のため）
            _squareToPatterns = new PatternMapping[BoardSize][];
            for (int i = 0; i < BoardSize; i++)
            {
                _squareToPatterns[i] = tempLists[i].ToArray();
            }
        }

        /// <summary>
        /// 指定されたマスに対応するパターン逆引き情報を取得します。
        /// </summary>
        /// <param name="square">マス（0-63）</param>
        /// <returns>そのマスを含む全パターンの逆引き情報</returns>
        public PatternMapping[] GetSquarePatterns(int square)
        {
            return _squareToPatterns[square];
        }

        /// <summary>
        /// _preallocatedResults を外部から参照するためのプロパティです。
        /// 差分更新ロジックが直接パターンインデックスを更新・復元するために使用します。
        /// </summary>
        public Dictionary<FeaturePattern.Type, int[]> PreallocatedResults => _preallocatedResults;

        /// <summary>
        /// 差分更新モードフラグ。
        /// true の場合、ExtractNoAlloc はフルスキャンを行わず、
        /// _preallocatedResults をそのまま返します（差分更新済みの値を使用）。
        /// PvsSearchEngine が探索開始時に true に設定し、探索完了後に false に戻します。
        /// </summary>
        public bool IncrementalMode { get; set; }

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
        /// IncrementalMode が true の場合、フルスキャンを行わず差分更新済みの _preallocatedResults をそのまま返します。
        /// IncrementalMode が false の場合、全パターンをフルスキャンして _preallocatedResults を更新して返します。
        /// 戻り値は内部バッファへの参照であり、次回呼び出しで上書きされます。
        /// シングルスレッド前提のメソッドです。
        /// </summary>
        /// <param name="context">盤状態</param>
        /// <returns>特徴パターンの辞書（内部バッファへの参照）</returns>
        public Dictionary<FeaturePattern.Type, int[]> ExtractNoAlloc(BoardContext context)
        {
            // 差分更新モード中はフルスキャンをスキップし、既に更新済みのバッファを返す
            if (IncrementalMode)
            {
                return _preallocatedResults;
            }

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
