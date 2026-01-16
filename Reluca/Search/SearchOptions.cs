/// <summary>
/// 【ModuleDoc】
/// 責務: 探索パラメータを保持するデータクラス
/// 入出力: なし（データ保持のみ）
/// 副作用: なし
///
/// Aspiration Window パラメータ:
/// - UseAspirationWindow: ON/OFF 切替（デフォルト OFF）
/// - AspirationDelta: 初期ウィンドウ幅（デフォルト 50）
/// - AspirationMaxRetry: 最大再探索回数（デフォルト 3）
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
        /// デフォルトの Aspiration Window 初期幅
        /// </summary>
        private const long DefaultAspirationDelta = 50;

        /// <summary>
        /// デフォルトの Aspiration 最大再探索回数
        /// </summary>
        private const int DefaultAspirationMaxRetry = 3;

        /// <summary>
        /// 最大探索深さ
        /// </summary>
        public int MaxDepth { get; }

        /// <summary>
        /// Transposition Table を使用するかどうか
        /// </summary>
        public bool UseTranspositionTable { get; }

        /// <summary>
        /// Aspiration Window を使用するかどうか
        /// </summary>
        public bool UseAspirationWindow { get; }

        /// <summary>
        /// Aspiration Window の初期幅（δ）
        /// </summary>
        public long AspirationDelta { get; }

        /// <summary>
        /// Aspiration の最大再探索回数
        /// </summary>
        public int AspirationMaxRetry { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxDepth">最大探索深さ（省略時はデフォルト値）</param>
        /// <param name="useTranspositionTable">Transposition Table を使用するか（省略時は false）</param>
        /// <param name="useAspirationWindow">Aspiration Window を使用するか（省略時は false）</param>
        /// <param name="aspirationDelta">Aspiration Window の初期幅（省略時は 50）</param>
        /// <param name="aspirationMaxRetry">Aspiration の最大再探索回数（省略時は 3）</param>
        public SearchOptions(
            int maxDepth = DefaultMaxDepth,
            bool useTranspositionTable = false,
            bool useAspirationWindow = false,
            long aspirationDelta = DefaultAspirationDelta,
            int aspirationMaxRetry = DefaultAspirationMaxRetry)
        {
            MaxDepth = maxDepth;
            UseTranspositionTable = useTranspositionTable;
            UseAspirationWindow = useAspirationWindow;
            AspirationDelta = aspirationDelta;
            AspirationMaxRetry = aspirationMaxRetry;
        }
    }
}
