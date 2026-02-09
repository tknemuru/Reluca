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
/// - AspirationUseStageTable: ステージ別 delta テーブルおよび指数拡張戦略の ON/OFF 切替（デフォルト OFF）
///
/// Multi-ProbCut パラメータ:
/// - UseMultiProbCut: ON/OFF 切替（デフォルト OFF）
///
/// 時間制御パラメータ:
/// - TimeLimitMs: 探索の制限時間（ミリ秒）。null の場合は時間制限なしで MaxDepth まで探索する
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
        /// Aspiration Window のステージ別 delta テーブルおよび指数拡張戦略を使用するかどうか。
        /// true の場合、AspirationDelta は無視され、ステージ別テーブルの値と指数拡張戦略が使用される。
        /// false の場合、既存の AspirationDelta と固定 2 倍拡張が使用される（後方互換）。
        /// </summary>
        public bool AspirationUseStageTable { get; }

        /// <summary>
        /// Multi-ProbCut を使用するかどうか
        /// </summary>
        public bool UseMultiProbCut { get; }

        /// <summary>
        /// 探索の制限時間（ミリ秒）。
        /// null の場合は時間制限なしで MaxDepth まで探索する。
        /// </summary>
        public long? TimeLimitMs { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="maxDepth">最大探索深さ（省略時はデフォルト値）</param>
        /// <param name="useTranspositionTable">Transposition Table を使用するか（省略時は false）</param>
        /// <param name="useAspirationWindow">Aspiration Window を使用するか（省略時は false）</param>
        /// <param name="aspirationDelta">Aspiration Window の初期幅（省略時は 50）</param>
        /// <param name="aspirationMaxRetry">Aspiration の最大再探索回数（省略時は 3）</param>
        /// <param name="aspirationUseStageTable">ステージ別 delta テーブルおよび指数拡張戦略を使用するか（省略時は false）</param>
        /// <param name="useMultiProbCut">Multi-ProbCut を使用するか（省略時は false）</param>
        /// <param name="timeLimitMs">探索の制限時間（ミリ秒）。null の場合は時間制限なし（省略時は null）</param>
        public SearchOptions(
            int maxDepth = DefaultMaxDepth,
            bool useTranspositionTable = false,
            bool useAspirationWindow = false,
            long aspirationDelta = DefaultAspirationDelta,
            int aspirationMaxRetry = DefaultAspirationMaxRetry,
            bool aspirationUseStageTable = false,
            bool useMultiProbCut = false,
            long? timeLimitMs = null)
        {
            MaxDepth = maxDepth;
            UseTranspositionTable = useTranspositionTable;
            UseAspirationWindow = useAspirationWindow;
            AspirationDelta = aspirationDelta;
            AspirationMaxRetry = aspirationMaxRetry;
            AspirationUseStageTable = aspirationUseStageTable;
            UseMultiProbCut = useMultiProbCut;
            TimeLimitMs = timeLimitMs;
        }
    }
}
