/// <summary>
/// 【ModuleDoc】
/// 責務: 探索結果を保持するデータクラス
/// 入出力: なし（データ保持のみ）
/// 副作用: なし
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// 探索結果を表します。
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// 最善手（0-63のインデックス、-1は無効）
        /// </summary>
        public int BestMove { get; }

        /// <summary>
        /// 評価値
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// 探索ノード数
        /// </summary>
        public long NodesSearched { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="bestMove">最善手</param>
        /// <param name="value">評価値</param>
        /// <param name="nodesSearched">探索ノード数</param>
        public SearchResult(int bestMove, long value, long nodesSearched = 0)
        {
            BestMove = bestMove;
            Value = value;
            NodesSearched = nodesSearched;
        }
    }
}
