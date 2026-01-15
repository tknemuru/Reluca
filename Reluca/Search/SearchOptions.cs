/// <summary>
/// 【ModuleDoc】
/// 責務: 探索パラメータを保持するデータクラス
/// 入出力: なし（データ保持のみ）
/// 副作用: なし
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// 探索オプションを表します。
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// デフォルトの探索深さ
        /// </summary>
        private const int DefaultMaxDepth = 7;

        /// <summary>
        /// 最大探索深さ
        /// </summary>
        public int MaxDepth { get; }

        /// <summary>
        /// Transposition Table を使用するかどうか
        /// </summary>
        public bool UseTranspositionTable { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxDepth">最大探索深さ（省略時はデフォルト値）</param>
        /// <param name="useTranspositionTable">Transposition Table を使用するか（省略時は false）</param>
        public SearchOptions(int maxDepth = DefaultMaxDepth, bool useTranspositionTable = false)
        {
            MaxDepth = maxDepth;
            UseTranspositionTable = useTranspositionTable;
        }
    }
}
