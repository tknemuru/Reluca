/// <summary>
/// 【ModuleDoc】
/// 責務: PVS（Principal Variation Search / NegaScout）アルゴリズムによる探索を提供する
/// 入出力: GameContext + SearchOptions + IEvaluable → SearchResult
/// 副作用: TT 使用時は ITranspositionTable の内容を更新
///
/// 設計方針:
/// - Template Method を使用せず、明示的な制御フローで PVS を実装
/// - コンストラクタ DI により依存を注入（DiProvider.Get() は使用しない）
/// - TT 統合は SearchOptions.UseTranspositionTable で切替可能
/// - 反復深化（Iterative Deepening）により depth=1..MaxDepth を順次探索
/// - TT Clear は Search セッション冒頭で1回のみ（反復ごとの Clear は禁止）
/// - MakeMove/UnmakeMove パターンにより盤面を in-place で変更・復元し、ヒープアロケーションを排除
/// - シングルスレッド前提で設計されている
///
/// RootSearch の責務:
/// - ルート局面の合法手列挙と手順序の最適化
/// - 手順序優先順位: 1) TT bestMove, 2) 前回反復の bestMove, 3) その他
/// - Move Ordering（評価値ソート）は既存条件（depth <= 6 相当）に従う
/// - ルートでの bestMove 更新を一元管理
///
/// Aspiration Window の責務:
/// - depth >= 2 で前回反復の評価値を中心に狭い窓で探索
/// - AspirationUseStageTable = true 時: ステージ別 delta テーブル + 深さ補正 + 指数拡張戦略
/// - AspirationUseStageTable = false 時: 固定 delta + 固定 2 倍拡張（従来動作）
/// - fail-low/high 時は δ を拡張して再探索（最大 AspirationMaxRetry 回）
/// - 再探索回数超過時はフルウィンドウにフォールバック（正しさ担保）
/// - fail-low/high 判定は初期窓境界（initialAlpha/initialBeta）で行う
/// - δ 拡大時はオーバーフロー防止のため MaxDelta で clamp
/// - TT Store を有効にして探索し、1回の RootSearch で完結する
/// - retry 発生回数とフォールバック回数を計測し、ログ出力する
///
/// Null Window Search (NWS) の責務:
/// - 最初の手（Principal Variation）のみフルウィンドウで探索
/// - 2 手目以降は Null Window (-alpha-1, -alpha) で探索
/// - fail-high 時のみフルウィンドウで再探索
/// - Move Ordering が適切であれば、大半の手は NWS で枝刈りされる
///
/// Multi-ProbCut (MPC) の責務:
/// - 浅い探索の評価値から深い探索の結果を統計的に予測し、枝刈りを行う
/// - TT Probe 後・合法手展開前に MPC 判定を挿入
/// - _mpcEnabled フラグにより MPC の再帰適用を防止（シングルスレッド前提）
/// - カットペアを深い方から順に評価し、最初にカット条件が成立した時点で枝刈り確定
/// - パス直後のノード（isPassed == true）では MPC を適用しない
///
/// 時間制御の責務:
/// - レイヤー 1: 反復深化ループの各深さ完了後に経過時間を判定し、次の深さの探索可否を制御
/// - レイヤー 2: ノード展開時に一定間隔でタイムアウトチェックを行い、SearchTimeoutException で中断
/// - depth=1 の探索中はレイヤー 2 を無効化し、最低 1 手の探索結果を保証
/// - TimeLimitMs 未指定時はタイムアウトチェックが無効化され、従来動作と同一
///
/// フェイルセーフ設計:
/// - MakeMove/UnmakeMove ペアは try/finally で囲み、例外発生時にも必ず UnmakeMove を実行
/// - Search メソッドでルート盤面をバックアップし、SearchTimeoutException 発生後に復元
/// </summary>
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Evaluates;
using Reluca.Models;
using Reluca.Search.Transposition;

namespace Reluca.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PVS（Principal Variation Search）アルゴリズムによる探索エンジンです。
    /// NegaScout とも呼ばれ、最初の手をフルウィンドウで探索し、
    /// 2手目以降は Null Window Search で枝刈りを試みます。
    /// 反復深化（Iterative Deepening）により効率的な探索を実現します。
    /// MakeMove/UnmakeMove パターンにより、探索中のヒープアロケーションを排除します。
    /// </summary>
    public class PvsSearchEngine : ISearchEngine
    {
        /// <summary>
        /// 初期アルファ値
        /// </summary>
        private const long DefaultAlpha = -1000000000000000001L;

        /// <summary>
        /// 初期ベータ値
        /// </summary>
        private const long DefaultBeta = 1000000000000000001L;

        /// <summary>
        /// Aspiration δ の最大値（オーバーフロー防止）
        /// </summary>
        private const long MaxDelta = DefaultBeta - DefaultAlpha;

        /// <summary>
        /// タイムアウトチェックのノード間隔。
        /// この値ごとに Stopwatch を確認する。2 のべき乗でビット AND による高速判定を行う。
        /// </summary>
        private const int TimeoutCheckInterval = 4096;

        /// <summary>
        /// ロガー
        /// </summary>
        private readonly ILogger<PvsSearchEngine> _logger;

        /// <summary>
        /// 着手可能数分析機能
        /// </summary>
        private readonly MobilityAnalyzer _mobilityAnalyzer;

        /// <summary>
        /// 置換表
        /// </summary>
        private readonly ITranspositionTable _transpositionTable;

        /// <summary>
        /// Zobrist ハッシュ計算機能
        /// </summary>
        private readonly IZobristHash _zobristHash;

        /// <summary>
        /// MPC パラメータテーブル
        /// </summary>
        private readonly MpcParameterTable _mpcParameterTable;

        /// <summary>
        /// Aspiration Window のステージ別パラメータテーブル
        /// </summary>
        private readonly AspirationParameterTable _aspirationParameterTable;

        /// <summary>
        /// 探索中の最善手
        /// </summary>
        private int _bestMove;

        /// <summary>
        /// 探索時の現在深さ（Move Ordering 判定用）
        /// </summary>
        private int _currentDepth;

        /// <summary>
        /// 探索時の評価関数
        /// </summary>
        private IEvaluable? _evaluator;

        /// <summary>
        /// 探索オプション
        /// </summary>
        private SearchOptions? _options;

        /// <summary>
        /// TT Store を抑制するフラグ（Aspiration retry 中は true）
        /// </summary>
        private bool _suppressTTStore;

        /// <summary>
        /// 探索ノード数カウンタ
        /// </summary>
        private long _nodesSearched;

        /// <summary>
        /// MPC の再帰適用を防止するフラグ。
        /// MPC 用の浅い探索内で再帰的に MPC が適用されることを防止する。
        /// </summary>
        private bool _mpcEnabled;

        /// <summary>
        /// MPC カット発生回数カウンタ
        /// </summary>
        private long _mpcCutCount;

        /// <summary>
        /// Aspiration retry 発生回数カウンタ（深さごと）
        /// </summary>
        private int _aspirationRetryCount;

        /// <summary>
        /// Aspiration フルウィンドウフォールバック発生回数カウンタ（深さごと）
        /// </summary>
        private int _aspirationFallbackCount;

#if DEBUG
        /// <summary>
        /// ビットボード合法手生成の呼び出し回数カウンタ（デバッグ用）
        /// </summary>
        private long _debugBitboardMoveGenCount;

        /// <summary>
        /// Zobrist ハッシュ差分更新の呼び出し回数カウンタ（デバッグ用）
        /// </summary>
        private long _debugZobristUpdateCount;

        /// <summary>
        /// Zobrist ハッシュフルスキャンの呼び出し回数カウンタ（デバッグ用）
        /// </summary>
        private long _debugZobristFullScanCount;

        /// <summary>
        /// パターンインデックス差分更新の呼び出し回数カウンタ（デバッグ用）
        /// </summary>
        private long _debugPatternDeltaUpdateCount;

        /// <summary>
        /// パターンインデックス差分更新で影響を受けたパターン数の合計（デバッグ用）。
        /// 平均影響パターン数の算出に使用します。
        /// </summary>
        private long _debugPatternDeltaAffectedTotal;
#endif

        /// <summary>
        /// 探索の開始時刻を計測するストップウォッチ
        /// </summary>
        private readonly Stopwatch _stopwatch = new();

        /// <summary>
        /// 探索の制限時間（ミリ秒）。null は制限なし
        /// </summary>
        private long? _timeLimitMs;

        /// <summary>
        /// 特徴パターン抽出機能。差分更新のための逆引きテーブルとパターンインデックスバッファを保持します。
        /// </summary>
        private readonly FeaturePatternExtractor _featurePatternExtractor;

        /// <summary>
        /// パターンインデックスの差分変更記録用バッファ。
        /// 探索全体で1つのバッファを共有し、各 MoveInfo が開始オフセットと件数を保持します。
        /// </summary>
        private PatternIndexChange[] _patternChangeBuffer;

        /// <summary>
        /// パターンインデックスの差分変更記録の現在のオフセット。
        /// MakeMove のたびにオフセットが進み、UnmakeMove で戻ります。
        /// </summary>
        private int _patternChangeOffset;

        /// <summary>
        /// 1手あたりの最大パターン変更数。
        /// 変化マス数（着手1 + 裏返し最大10 = 11）× 影響パターン数（最大8）= 88。
        /// 安全マージンとして 2 のべき乗に切り上げます。
        /// </summary>
        private const int MaxChangesPerMove = 128;

        /// <summary>
        /// 最大探索深さ。パターン変更バッファのサイズ決定に使用します。
        /// </summary>
        private const int MaxSearchDepth = 64;

        /// <summary>
        /// パターンインデックスの差分変更記録です。
        /// UnmakeMove 時に _preallocatedResults を復元するために使用します。
        /// </summary>
        private struct PatternIndexChange
        {
            /// <summary>
            /// パターンの種類
            /// </summary>
            public FeaturePattern.Type PatternType;

            /// <summary>
            /// サブパターンインデックス
            /// </summary>
            public int SubPatternIndex;

            /// <summary>
            /// 変更前のインデックス値
            /// </summary>
            public int PrevIndex;
        }

        /// <summary>
        /// 着手情報を保持する構造体。スタック上に配置され、ヒープアロケーションは発生しない。
        /// MakeMove で着手前の状態を保存し、UnmakeMove で復元する際に使用する。
        /// </summary>
        private struct MoveInfo
        {
            /// <summary>
            /// 着手前の黒石配置
            /// </summary>
            public ulong PrevBlack;

            /// <summary>
            /// 着手前の白石配置
            /// </summary>
            public ulong PrevWhite;

            /// <summary>
            /// 着手前の手番
            /// </summary>
            public Disc.Color PrevTurn;

            /// <summary>
            /// 着手前のターン数
            /// </summary>
            public int PrevTurnCount;

            /// <summary>
            /// 着手前のステージ
            /// </summary>
            public int PrevStage;

            /// <summary>
            /// 着手前の指し手
            /// </summary>
            public int PrevMove;

            /// <summary>
            /// 着手前の配置可能状態
            /// </summary>
            public ulong PrevMobility;

            /// <summary>
            /// 裏返された石のビットボード。
            /// Zobrist ハッシュ差分更新および評価関数差分更新で使用します。
            /// </summary>
            public ulong Flipped;

            /// <summary>
            /// パターン変更バッファの開始オフセット。
            /// UnmakeMove 時にこのオフセットから復元します。
            /// </summary>
            public int PatternChangeStart;

            /// <summary>
            /// パターン変更の件数。
            /// </summary>
            public int PatternChangeCount;
        }

        /// <summary>
        /// コンストラクタ。DI から依存を注入します。
        /// </summary>
        /// <param name="logger">ロガー</param>
        /// <param name="mobilityAnalyzer">着手可能数分析機能</param>
        /// <param name="transpositionTable">置換表</param>
        /// <param name="zobristHash">Zobrist ハッシュ計算機能</param>
        /// <param name="mpcParameterTable">MPC パラメータテーブル</param>
        /// <param name="aspirationParameterTable">Aspiration Window のステージ別パラメータテーブル</param>
        /// <param name="featurePatternExtractor">特徴パターン抽出機能（差分更新に使用）</param>
        public PvsSearchEngine(
            ILogger<PvsSearchEngine> logger,
            MobilityAnalyzer mobilityAnalyzer,
            ITranspositionTable transpositionTable,
            IZobristHash zobristHash,
            MpcParameterTable mpcParameterTable,
            AspirationParameterTable aspirationParameterTable,
            FeaturePatternExtractor featurePatternExtractor)
        {
            _logger = logger;
            _mobilityAnalyzer = mobilityAnalyzer;
            _transpositionTable = transpositionTable;
            _zobristHash = zobristHash;
            _mpcParameterTable = mpcParameterTable;
            _aspirationParameterTable = aspirationParameterTable;
            _featurePatternExtractor = featurePatternExtractor;
            _patternChangeBuffer = new PatternIndexChange[MaxSearchDepth * MaxChangesPerMove];
            _bestMove = -1;
        }

        /// <summary>
        /// 指定されたゲーム状態から最善手を探索します。
        /// 反復深化（Iterative Deepening）により depth=1 から MaxDepth まで順次探索します。
        /// Aspiration Window 有効時は depth >= 2 で狭い窓から探索を開始します。
        /// MakeMove/UnmakeMove パターンにより盤面を in-place で変更するため、
        /// SearchTimeoutException 発生時にはルート盤面のバックアップから復元します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="options">探索オプション</param>
        /// <param name="evaluator">評価関数</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
        {
            _bestMove = -1;
            _evaluator = evaluator;
            _options = options;
            _mpcEnabled = options.UseMultiProbCut;
            _timeLimitMs = options.TimeLimitMs;

            // ルート盤面のバックアップ（SearchTimeoutException 発生時の復元用）
            var rootBackup = new MoveInfo
            {
                PrevBlack = context.Black,
                PrevWhite = context.White,
                PrevTurn = context.Turn,
                PrevTurnCount = context.TurnCount,
                PrevStage = context.Stage,
                PrevMove = context.Move,
                PrevMobility = context.Mobility,
            };

            // パターンインデックスの初期フルスキャンと差分更新モードの設定
            _featurePatternExtractor.ExtractNoAlloc(context.Board);
            _featurePatternExtractor.IncrementalMode = true;
            _patternChangeOffset = 0;

#if DEBUG
            // デバッグカウンタの初期化
            _debugBitboardMoveGenCount = 0;
            _debugZobristUpdateCount = 0;
            _debugZobristFullScanCount = 0;
            _debugPatternDeltaUpdateCount = 0;
            _debugPatternDeltaAffectedTotal = 0;
#endif

            // ストップウォッチ開始
            _stopwatch.Restart();

            // TT 使用時は探索開始前に1回だけクリア（反復ごとの Clear は禁止）
            if (options.UseTranspositionTable)
            {
                _transpositionTable.Clear();
            }

            // 反復深化: depth=1 から MaxDepth まで
            SearchResult result = new SearchResult(-1, 0);
            long prevValue = 0;
            long totalNodesSearched = 0;
            int completedDepth = 0;
            long prevDepthElapsedMs = 0;

            for (int depth = 1; depth <= options.MaxDepth; depth++)
            {
                // レイヤー 1: 次の深さを探索するか判定
                if (depth >= 2 && _timeLimitMs.HasValue)
                {
                    long elapsedMs = _stopwatch.ElapsedMilliseconds;
                    long remainingMs = _timeLimitMs.Value - elapsedMs;

                    // 前回の深さにかかった時間から次の深さの所要時間を推定
                    // 分岐係数を考慮し、次の深さは前回の約 3 倍の時間を要すると推定
                    long estimatedNextMs = prevDepthElapsedMs * 3;

                    if (estimatedNextMs > remainingMs)
                    {
                        _logger.LogInformation(
                            "時間制御により探索を打ち切り {@TimeControl}",
                            new
                            {
                                CompletedDepth = completedDepth,
                                ElapsedMs = elapsedMs,
                                TimeLimitMs = _timeLimitMs.Value,
                                EstimatedNextMs = estimatedNextMs,
                                RemainingMs = remainingMs
                            });
                        break;
                    }
                }

                _currentDepth = depth;
                _nodesSearched = 0;
                _mpcCutCount = 0;
                _aspirationRetryCount = 0;
                _aspirationFallbackCount = 0;

                long depthStartMs = _stopwatch.ElapsedMilliseconds;

                try
                {
                    SearchResult depthResult;
                    // Aspiration Window: depth >= 2 かつ UseAspirationWindow=true の場合
                    if (depth >= 2 && options.UseAspirationWindow)
                    {
                        depthResult = AspirationRootSearch(context, depth, prevValue);
                    }
                    else
                    {
                        // depth=1 またはAspiration OFF: フルウィンドウで探索
                        depthResult = RootSearch(context, depth, DefaultAlpha, DefaultBeta);
                    }

                    // 探索成功: 結果を更新
                    result = depthResult;
                    completedDepth = depth;
                }
                catch (SearchTimeoutException)
                {
                    // レイヤー 2: 探索途中でタイムアウト
                    // ルート盤面を復元（MakeMove/UnmakeMove の途中で例外が発生した可能性がある）
                    RestoreContext(context, rootBackup);

                    // パターンインデックスを復元（差分更新が中途半端な状態の可能性がある）
                    _featurePatternExtractor.IncrementalMode = false;
                    _featurePatternExtractor.ExtractNoAlloc(context.Board);
                    _featurePatternExtractor.IncrementalMode = true;
                    _patternChangeOffset = 0;

                    // 直前の深さの結果を返す（result は更新しない）
                    // タイムアウト発生時の深さで探索されたノード数も集計に含める
                    totalNodesSearched += _nodesSearched;
                    _logger.LogInformation(
                        "探索途中でタイムアウト {@Timeout}",
                        new
                        {
                            InterruptedDepth = depth,
                            CompletedDepth = completedDepth,
                            ElapsedMs = _stopwatch.ElapsedMilliseconds,
                            TimeLimitMs = _timeLimitMs.Value
                        });
                    break;
                }

                long depthElapsedMs = _stopwatch.ElapsedMilliseconds - depthStartMs;
                prevDepthElapsedMs = depthElapsedMs;

                totalNodesSearched += _nodesSearched;
                _logger.LogInformation("探索進捗 {@SearchProgress}", new
                {
                    Depth = depth,
                    Nodes = _nodesSearched,
                    TotalNodes = totalNodesSearched,
                    Value = result.Value,
                    MpcCuts = _mpcCutCount,
                    AspirationRetries = _aspirationRetryCount,
                    AspirationFallbacks = _aspirationFallbackCount,
                    DepthElapsedMs = depthElapsedMs
                });

                prevValue = result.Value;
            }

            _stopwatch.Stop();

            // 差分更新モードを終了
            _featurePatternExtractor.IncrementalMode = false;

#if DEBUG
            // デバッグカウンタのログ出力
            double debugPatternDeltaAvgAffected = _debugPatternDeltaUpdateCount > 0
                ? (double)_debugPatternDeltaAffectedTotal / _debugPatternDeltaUpdateCount
                : 0;
            _logger.LogInformation("Phase3 デバッグカウンタ {@DebugCounters}", new
            {
                BitboardMoveGenCount = _debugBitboardMoveGenCount,
                ZobristUpdateCount = _debugZobristUpdateCount,
                ZobristFullScanCount = _debugZobristFullScanCount,
                PatternDeltaUpdateCount = _debugPatternDeltaUpdateCount,
                PatternDeltaAvgAffected = debugPatternDeltaAvgAffected,
            });
#endif

            return new SearchResult(
                result.BestMove,
                result.Value,
                totalNodesSearched,
                completedDepth,
                _stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Aspiration Window を使用したルート探索を行います。
        /// 前回反復の評価値を中心に狭い窓で探索し、fail-low/high 時は窓を広げて再探索します。
        /// TT Store を有効にして 1 回の RootSearch で完結させます。
        /// fail 時に書き込まれた TT エントリは BoundType により適切にフィルタリングされるため、
        /// TT Store を抑制する必要はありません。
        /// AspirationUseStageTable = true の場合、ステージ別 delta テーブルと指数拡張戦略を使用します。
        /// AspirationUseStageTable = false の場合、固定 delta と固定 2 倍拡張を使用します（従来動作）。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="prevValue">前回反復の評価値</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        private SearchResult AspirationRootSearch(GameContext context, int depth, long prevValue)
        {
            // delta 初期値の決定
            long delta;
            bool useExponentialExpansion;
            if (_options!.AspirationUseStageTable)
            {
                long baseDelta = _aspirationParameterTable.GetDelta(context.Stage);
                delta = AspirationParameterTable.GetAdjustedDelta(baseDelta, depth);
                useExponentialExpansion = true;
            }
            else
            {
                delta = _options.AspirationDelta;
                useExponentialExpansion = false;
            }

            int retryCount = 0;

            while (retryCount <= _options.AspirationMaxRetry)
            {
                // 初期窓を設定（判定用に保存）
                long initialAlpha = prevValue - delta;
                long initialBeta = prevValue + delta;

                // 窓が DefaultAlpha/DefaultBeta を超えないように clamp
                long alpha = Math.Max(initialAlpha, DefaultAlpha);
                long beta = Math.Min(initialBeta, DefaultBeta);

                // TT Store を有効にして探索（1回のみ）
                _suppressTTStore = false;
                var result = RootSearch(context, depth, alpha, beta);

                // fail-low/high 判定は初期窓境界で行う
                if (result.Value <= initialAlpha || result.Value >= initialBeta)
                {
                    // fail-low または fail-high: delta を拡張して再探索
                    delta = ExpandDelta(delta, retryCount, useExponentialExpansion);
                    retryCount++;
                    _aspirationRetryCount++;
                }
                else
                {
                    // 成功: 結果をそのまま返す（TT Store は RootSearch 内で完了済み）
                    return result;
                }
            }

            // フォールバック: 再探索回数超過 → フルウィンドウで探索（正しさ担保）
            _aspirationFallbackCount++;
            _suppressTTStore = false;
            return RootSearch(context, depth, DefaultAlpha, DefaultBeta);
        }

        /// <summary>
        /// delta の拡張を行う。指数拡張と固定 2 倍拡張を切り替え可能。
        /// fail-low / fail-high の両方から共通で呼び出される。
        /// </summary>
        /// <param name="delta">現在の delta</param>
        /// <param name="retryCount">現在の retry 回数（0 始まり）</param>
        /// <param name="useExponentialExpansion">指数拡張を使用するかどうか</param>
        /// <returns>拡張後の delta</returns>
        internal static long ExpandDelta(long delta, int retryCount, bool useExponentialExpansion)
        {
            if (useExponentialExpansion)
            {
                // 指数拡張（2^(retryCount+1) 倍）
                long expansionFactor = 1L << (retryCount + 1);
                return ClampDelta(delta, expansionFactor);
            }
            else
            {
                // 固定 2 倍拡張（従来動作）- ClampDelta で統一的にオーバーフロー防止
                return ClampDelta(delta, 2);
            }
        }

        /// <summary>
        /// delta に拡張倍率を適用し、オーバーフロー防止のためクランプする。
        /// 乗算前にオーバーフローチェックを行い、オーバーフローする場合は MaxDelta を返す。
        /// </summary>
        /// <param name="delta">現在の delta</param>
        /// <param name="factor">拡張倍率（1 以上であること）</param>
        /// <returns>クランプ後の delta</returns>
        internal static long ClampDelta(long delta, long factor)
        {
            Debug.Assert(factor > 0, "factor must be positive to avoid division by zero");

            // オーバーフロー防止: delta * factor が long.MaxValue を超える場合は MaxDelta を返す
            if (delta > MaxDelta / factor)
            {
                return MaxDelta;
            }
            return Math.Min(delta * factor, MaxDelta);
        }

        /// <summary>
        /// ルート局面での探索を行います。
        /// 手順序の最適化と bestMove の更新を一元管理します。
        /// MakeMove/UnmakeMove パターンにより盤面を in-place で変更・復元します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="alpha">アルファ値（下界）</param>
        /// <param name="beta">ベータ値（上界）</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        private SearchResult RootSearch(GameContext context, int depth, long alpha, long beta)
        {
            // ルート局面の合法手を取得
            var moves = _mobilityAnalyzer.Analyze(context);
#if DEBUG
            _debugBitboardMoveGenCount++;
#endif
            if (moves.Count == 0)
            {
                return new SearchResult(-1, Evaluate(context));
            }

            // ルートのハッシュを計算（TT Probe/Store および差分更新の起点として使用）
            ulong rootHash = 0;
            if (_options?.UseTranspositionTable == true)
            {
                rootHash = _zobristHash.ComputeHash(context);
#if DEBUG
                _debugZobristFullScanCount++;
#endif
            }

            // 手順序の最適化（優先順位: 1) TT bestMove, 2) 前回反復の bestMove, 3) その他）
            moves = OptimizeMoveOrder(moves, context, depth, rootHash);

            // originalAlpha を保存（BoundType 判定用）
            long originalAlpha = alpha;
            long maxValue = DefaultAlpha;
            int rootBestMove = moves[0]; // 最初の手をデフォルトとする

            foreach (var move in moves)
            {
                var moveInfo = MakeMove(context, move);
                long score;
                try
                {
                    // Zobrist ハッシュの差分更新（MakeMove 後に子局面のハッシュを計算）
                    ulong childHash = _options?.UseTranspositionTable == true
                        ? _zobristHash.UpdateHash(rootHash, move, moveInfo.Flipped, moveInfo.PrevTurn == Disc.Color.Black)
                        : 0;
#if DEBUG
                    if (_options?.UseTranspositionTable == true)
                        _debugZobristUpdateCount++;
#endif

                    // 再帰探索
                    // depth=1 の時 remainingDepth=0 で即評価
                    // depth=N の時 remainingDepth=N-1 で (N-1) レベル探索
                    score = -Pvs(context, depth - 1, -beta, -alpha, false, childHash);
                }
                finally
                {
                    UnmakeMove(context, moveInfo);
                }

                if (score > maxValue)
                {
                    maxValue = score;
                    rootBestMove = move;
                    alpha = Math.Max(alpha, maxValue);
                }
            }

            _bestMove = rootBestMove;

            // TT Store（ルート局面）- 正しい BoundType を判定
            // Aspiration retry 中は Store を抑制
            if (_options?.UseTranspositionTable == true && !_suppressTTStore)
            {
                var boundType = DetermineBoundType(maxValue, originalAlpha, beta);
                _transpositionTable.Store(rootHash, depth, maxValue, boundType, rootBestMove);
            }

            return new SearchResult(rootBestMove, maxValue);
        }

        /// <summary>
        /// 手順序を最適化します。
        /// 優先順位: 1) TT bestMove, 2) 前回反復の bestMove, 3) その他（条件付きで評価値ソート）
        /// </summary>
        /// <param name="moves">合法手リスト</param>
        /// <param name="context">ゲーム状態</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="currentHash">現在局面の Zobrist ハッシュ値（呼び出し元で計算済み）</param>
        /// <returns>最適化された手順序</returns>
        private List<int> OptimizeMoveOrder(List<int> moves, GameContext context, int depth, ulong currentHash)
        {
            int? ttMove = null;
            int? prevBestMove = null;

            // 1) TT bestMove を取得（ハッシュは呼び出し元から受け取る）
            if (_options?.UseTranspositionTable == true)
            {
                int ttBestMove = _transpositionTable.GetBestMove(currentHash);
                if (ttBestMove != TTEntry.NoBestMove && moves.Contains(ttBestMove))
                {
                    ttMove = ttBestMove;
                }
            }

            // 2) 前回反復の bestMove
            if (_bestMove != -1 && moves.Contains(_bestMove) && _bestMove != ttMove)
            {
                prevBestMove = _bestMove;
            }

            // 3) Move Ordering（評価値ソート）- 既存条件に従う
            if (ShouldOrder(depth - 1))
            {
                OrderMoves(moves, context);
            }

            // 優先手を先頭に移動（後から挿入するので逆順で処理）
            if (prevBestMove.HasValue)
            {
                moves.Remove(prevBestMove.Value);
                moves.Insert(0, prevBestMove.Value);
            }

            if (ttMove.HasValue)
            {
                moves.Remove(ttMove.Value);
                moves.Insert(0, ttMove.Value);
            }

            return moves;
        }

        /// <summary>
        /// PVS（Principal Variation Search）の再帰探索メソッドです。
        /// ルート以外のノードで使用します。
        /// 最初の手をフルウィンドウで探索し、2 手目以降は Null Window Search で枝刈りを試みます。
        /// MakeMove/UnmakeMove パターンにより盤面を in-place で変更・復元します。
        /// Zobrist ハッシュは呼び出し元で差分更新された値を受け取り、再計算を回避します。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="remainingDepth">残り探索深さ（0 で評価）</param>
        /// <param name="alpha">アルファ値（下界）</param>
        /// <param name="beta">ベータ値（上界）</param>
        /// <param name="isPassed">直前がパスだったかどうか</param>
        /// <param name="currentHash">現在局面の Zobrist ハッシュ値（差分更新済み）</param>
        /// <returns>評価値</returns>
        private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed, ulong currentHash)
        {
            _nodesSearched++;

            // タイムアウトチェック（一定ノード数ごと、ただし depth=1 では無効化）
            if (_currentDepth >= 2
                && _timeLimitMs.HasValue
                && (_nodesSearched & (TimeoutCheckInterval - 1)) == 0)
            {
                if (_stopwatch.ElapsedMilliseconds >= _timeLimitMs.Value)
                {
                    throw new SearchTimeoutException();
                }
            }

            // 終了条件: 残り深さ 0 または終局
            if (remainingDepth == 0 || BoardAccessor.IsGameEndTurnCount(context))
            {
                return Evaluate(context);
            }

            // originalAlpha を保存（Store 時の BoundType 判定用）
            long originalAlpha = alpha;

            // TT Probe（ハッシュは呼び出し元から差分更新で受け取る）
            if (_options?.UseTranspositionTable == true)
            {
                if (_transpositionTable.TryProbe(currentHash, remainingDepth, alpha, beta, out var entry))
                {
                    return entry.Value;
                }
            }

            // MPC 判定（TT Probe 後・合法手展開前に挿入）
            // パス直後のノードでは MPC を適用しない（局面の性質が大きく変化しているため）
            if (_mpcEnabled && !isPassed)
            {
                var mpcResult = TryMultiProbCut(context, remainingDepth, alpha, beta, currentHash);
                if (mpcResult.HasValue)
                {
                    return mpcResult.Value;
                }
            }

            // 合法手を取得
            var moves = _mobilityAnalyzer.Analyze(context);
#if DEBUG
            _debugBitboardMoveGenCount++;
#endif

            long maxValue = DefaultAlpha;
            int localBestMove = TTEntry.NoBestMove;

            if (moves.Count > 0)
            {
                // TT の bestMove を先頭に移動（Move Ordering 改善）
                if (_options?.UseTranspositionTable == true)
                {
                    int ttMove = _transpositionTable.GetBestMove(currentHash);
                    if (ttMove != TTEntry.NoBestMove && moves.Contains(ttMove))
                    {
                        moves.Remove(ttMove);
                        moves.Insert(0, ttMove);
                    }
                }

                // Move Ordering（既存と同じ条件: depth <= 6 相当）
                if (ShouldOrder(remainingDepth))
                {
                    OrderMoves(moves, context);
                }

                bool isFirstMove = true;
                foreach (var move in moves)
                {
                    // 着手を実行（in-place）
                    var moveInfo = MakeMove(context, move);
                    long score;
                    try
                    {
                        // Zobrist ハッシュの差分更新（MakeMove 後に子局面のハッシュを計算）
                        ulong childHash = _options?.UseTranspositionTable == true
                            ? _zobristHash.UpdateHash(currentHash, move, moveInfo.Flipped, moveInfo.PrevTurn == Disc.Color.Black)
                            : 0;
#if DEBUG
                        if (_options?.UseTranspositionTable == true)
                            _debugZobristUpdateCount++;
#endif

                        if (isFirstMove)
                        {
                            // 最初の手: フルウィンドウで探索
                            score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false, childHash);
                            isFirstMove = false;
                        }
                        else
                        {
                            // 2手目以降: Null Window Search
                            score = -Pvs(context, remainingDepth - 1, -alpha - 1, -alpha, false, childHash);
                            if (score > alpha && score < beta)
                            {
                                // fail-high: フルウィンドウで再探索
                                score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false, childHash);
                            }
                        }
                    }
                    finally
                    {
                        UnmakeMove(context, moveInfo);
                    }

                    // ベータカット
                    if (score >= beta)
                    {
                        // TT Store（LowerBound）- Aspiration retry 中は Store を抑制
                        if (_options?.UseTranspositionTable == true && !_suppressTTStore)
                        {
                            _transpositionTable.Store(currentHash, remainingDepth, score, BoundType.LowerBound, move);
                        }

                        return score;
                    }

                    // より良い手が見つかった
                    if (score > maxValue)
                    {
                        maxValue = score;
                        localBestMove = move;

                        // アルファ値の更新
                        alpha = Math.Max(alpha, maxValue);
                    }
                }
            }
            else
            {
                // パス処理: Turn を反転して再帰探索し、戻った後に Turn を復元する。
                // MakeMove/UnmakeMove パターンと一貫した例外安全性を確保するため try/finally を使用する。
                // パス時のハッシュ更新: 盤面は変わらず手番のみ反転するため、TurnKey を XOR する
                var prevTurn = context.Turn;
                BoardAccessor.Pass(context);
                ulong passHash = currentHash ^ ZobristKeys.TurnKey;
                try
                {
                    maxValue = -Pvs(context, remainingDepth - 1, -beta, -alpha, true, passHash);
                }
                finally
                {
                    context.Turn = prevTurn;
                }
            }

            // TT Store - Aspiration retry 中は Store を抑制
            if (_options?.UseTranspositionTable == true && !_suppressTTStore && moves.Count > 0)
            {
                var boundType = DetermineBoundType(maxValue, originalAlpha, beta);
                _transpositionTable.Store(currentHash, remainingDepth, maxValue, boundType, localBestMove);
            }

            return maxValue;
        }

        /// <summary>
        /// Multi-ProbCut による枝刈り判定を行う。
        /// カットペアを深い方から順に評価し、カット条件が成立した場合はカット値を返す。
        /// カット条件が成立しない場合は null を返す。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="remainingDepth">残り探索深さ</param>
        /// <param name="alpha">アルファ値</param>
        /// <param name="beta">ベータ値</param>
        /// <param name="currentHash">現在局面の Zobrist ハッシュ値（差分更新済み）</param>
        /// <returns>カット値。カットしない場合は null</returns>
        private long? TryMultiProbCut(GameContext context, int remainingDepth, long alpha, long beta, ulong currentHash)
        {
            var cutPairs = _mpcParameterTable.CutPairs;
            double zValue = _mpcParameterTable.ZValue;

            // カットペアを深い方から順に判定
            for (int i = cutPairs.Count - 1; i >= 0; i--)
            {
                var pair = cutPairs[i];

                // 適用条件: remainingDepth >= deepDepth
                if (remainingDepth < pair.DeepDepth)
                {
                    continue;
                }

                var parameters = _mpcParameterTable.GetParameters(context.Stage, i);
                if (parameters == null)
                {
                    continue;
                }

                double a = parameters.A;
                double b = parameters.B;
                double sigma = parameters.Sigma;

                // 浅い探索を実行（MPC 用の探索では MPC を再帰適用しない）
                // Aspiration retry 中の _suppressTTStore も一時的に解除する
                // （MPC 用浅い探索は独立した探索であり、Aspiration retry の TT Store 抑制の影響を受けるべきではない）
                bool savedMpcFlag = _mpcEnabled;
                bool savedSuppressTTStore = _suppressTTStore;
                _mpcEnabled = false;
                _suppressTTStore = false;
                long shallowValue = Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false, currentHash);
                _mpcEnabled = savedMpcFlag;
                _suppressTTStore = savedSuppressTTStore;

                // Beta カット判定: shallowValue >= (zValue * sigma + beta - b) / a
                double betaThreshold = (zValue * sigma + (double)beta - b) / a;
                if (shallowValue >= (long)Math.Ceiling(betaThreshold))
                {
                    _mpcCutCount++;
                    return beta; // beta カット
                }

                // Alpha カット判定: shallowValue <= (-zValue * sigma + alpha - b) / a
                double alphaThreshold = (-zValue * sigma + (double)alpha - b) / a;
                if (shallowValue <= (long)Math.Floor(alphaThreshold))
                {
                    _mpcCutCount++;
                    return alpha; // alpha カット（fail-low）
                }
            }

            return null; // カットなし
        }

        /// <summary>
        /// BoundType を決定します。
        /// </summary>
        /// <param name="score">探索結果の評価値</param>
        /// <param name="originalAlpha">探索開始時の alpha 値</param>
        /// <param name="beta">探索開始時の beta 値</param>
        /// <returns>BoundType</returns>
        private static BoundType DetermineBoundType(long score, long originalAlpha, long beta)
        {
            if (score <= originalAlpha)
                return BoundType.UpperBound;
            if (score >= beta)
                return BoundType.LowerBound;
            return BoundType.Exact;
        }

        /// <summary>
        /// 局面を評価します。手番に応じたパリティを適用します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>評価値（現在の手番から見た値）</returns>
        private long Evaluate(GameContext context)
        {
            var score = _evaluator.Evaluate(context);
            var parity = context.Turn == Disc.Color.Black ? 1L : -1L;
            return score * parity;
        }

        /// <summary>
        /// 指し手を実行し、盤面を in-place で更新します。
        /// ビットボード演算で裏返し石を計算し、盤面を直接更新します。
        /// 着手前の状態を MoveInfo 構造体に保存して返します。
        /// 呼び出し側は探索完了後に UnmakeMove で必ず盤面を復元する必要があります。
        ///
        /// フィールド復元の完全性について:
        /// - ビットボード演算により Black, White を直接更新する
        /// - BoardAccessor.NextTurn は Turn, TurnCount, Stage を変更する
        /// - BoardAccessor.Pass は Turn のみを変更する（OrderMoves 内で追加呼び出しされるケースがある）
        /// - MoveInfo は上記すべてのフィールド（Black, White, Turn, TurnCount, Stage, Move, Mobility）を保存するため、
        ///   UnmakeMove で完全に復元可能である
        ///
        /// 【重要】このメソッド内でタイムアウトチェック（ThrowIfTimeout / SearchTimeoutException のスロー）を
        /// 行ってはならない。パターンインデックスの差分更新（UpdatePatternIndicesForSquare）が中途半端な状態で
        /// 例外が発生すると、_patternChangeOffset が不整合な状態となり、復元処理が正しく機能しなくなる。
        /// タイムアウトチェックは Pvs メソッド冒頭でのみ行うこと。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="move">指し手</param>
        /// <returns>着手前の状態情報（UnmakeMove に渡して復元する）</returns>
        private MoveInfo MakeMove(GameContext context, int move)
        {
            // 着手前の状態を保存
            var info = new MoveInfo
            {
                PrevBlack = context.Black,
                PrevWhite = context.White,
                PrevTurn = context.Turn,
                PrevTurnCount = context.TurnCount,
                PrevStage = context.Stage,
                PrevMove = context.Move,
                PrevMobility = context.Mobility,
            };

            // ビットボード演算で裏返し石を計算
            var (player, opponent) = context.Turn == Disc.Color.Black
                ? (context.Black, context.White)
                : (context.White, context.Black);
            ulong flipped = Analyzers.BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);

            // flipped を MoveInfo に保存（Zobrist 差分更新・評価関数差分更新で使用）
            info.Flipped = flipped;

            // パターンインデックスの差分更新
            // 着手位置: 空(Empty=1) → 手番色（Black=2 or White=0）
            // 裏返し位置: 相手色(White=0 or Black=2) → 手番色（Black=2 or White=0）
            bool isBlackTurn = context.Turn == Disc.Color.Black;
            info.PatternChangeStart = _patternChangeOffset;
            int changeCount = 0;

            // 着手位置の差分 (Empty → Turn)
            // Black: delta = (2 - 1) * weight = +weight
            // White: delta = (0 - 1) * weight = -weight
            int moveDelta = isBlackTurn ? 1 : -1; // 1 = (Black-Empty), -1 = (White-Empty)
            changeCount += UpdatePatternIndicesForSquare(move, moveDelta);

            // 裏返し位置の差分 (Opponent → Turn)
            // Black turn: White(0) → Black(2), delta = +2 * weight
            // White turn: Black(2) → White(0), delta = -2 * weight
            int flipDelta = isBlackTurn ? 2 : -2;
            ulong tmpFlipped = flipped;
            while (tmpFlipped != 0)
            {
                int sq = BitOperations.TrailingZeroCount(tmpFlipped);
                changeCount += UpdatePatternIndicesForSquare(sq, flipDelta);
                tmpFlipped &= tmpFlipped - 1;
            }
            info.PatternChangeCount = changeCount;
#if DEBUG
            _debugPatternDeltaUpdateCount++;
            _debugPatternDeltaAffectedTotal += changeCount;
#endif

            // ビットボード演算で盤面を更新
            ulong moveBit = 1UL << move;
            player |= moveBit | flipped;    // 着手位置 + 裏返し石を自石に追加
            opponent &= ~flipped;           // 裏返し石を相手石から除去

            // コンテキストに反映
            if (context.Turn == Disc.Color.Black)
            {
                context.Black = player;
                context.White = opponent;
            }
            else
            {
                context.White = player;
                context.Black = opponent;
            }

            context.Move = move;
            BoardAccessor.NextTurn(context);

            return info;
        }

        /// <summary>
        /// 指定マスの変化に伴い、影響を受ける全パターンのインデックスを差分更新します。
        /// 変更前のインデックスをバッファに記録し、UnmakeMove での復元に備えます。
        /// </summary>
        /// <param name="square">変化したマス（0-63）</param>
        /// <param name="deltaMultiplier">差分の方向と大きさ（+1: 空→黒, -1: 空→白, +2: 白→黒, -2: 黒→白）</param>
        /// <returns>記録した変更件数</returns>
        private int UpdatePatternIndicesForSquare(int square, int deltaMultiplier)
        {
            var mappings = _featurePatternExtractor.GetSquarePatterns(square);
            var results = _featurePatternExtractor.PreallocatedResults;
            int count = 0;

            for (int i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                var arr = results[mapping.PatternType];
                int prevIndex = arr[mapping.SubPatternIndex];

                // 変更前のインデックスをバッファに記録
                Debug.Assert(_patternChangeOffset < _patternChangeBuffer.Length,
                    $"パターン変更バッファがオーバーフローしています: offset={_patternChangeOffset}");
                _patternChangeBuffer[_patternChangeOffset] = new PatternIndexChange
                {
                    PatternType = mapping.PatternType,
                    SubPatternIndex = mapping.SubPatternIndex,
                    PrevIndex = prevIndex,
                };
                _patternChangeOffset++;
                count++;

                // インデックスを差分更新
                arr[mapping.SubPatternIndex] = prevIndex + deltaMultiplier * mapping.TernaryWeight;
            }

            return count;
        }

        /// <summary>
        /// 盤面を着手前の状態に復元します。
        /// MakeMove で保存した MoveInfo を使用して、全てのフィールドを元の値に戻します。
        /// パターンインデックスも変更記録から逆順に復元します。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="info">MakeMove で保存した着手前の状態情報</param>
        private void UnmakeMove(GameContext context, MoveInfo info)
        {
            context.Black = info.PrevBlack;
            context.White = info.PrevWhite;
            context.Turn = info.PrevTurn;
            context.TurnCount = info.PrevTurnCount;
            context.Stage = info.PrevStage;
            context.Move = info.PrevMove;
            context.Mobility = info.PrevMobility;

            // パターンインデックスを逆順に復元
            var results = _featurePatternExtractor.PreallocatedResults;
            int end = info.PatternChangeStart + info.PatternChangeCount;
            for (int i = end - 1; i >= info.PatternChangeStart; i--)
            {
                var change = _patternChangeBuffer[i];
                results[change.PatternType][change.SubPatternIndex] = change.PrevIndex;
            }
            _patternChangeOffset = info.PatternChangeStart;
        }

        /// <summary>
        /// ゲーム状態を MoveInfo から復元します。
        /// SearchTimeoutException 発生時のフォールバック用です。
        /// </summary>
        /// <param name="context">復元対象のゲーム状態</param>
        /// <param name="backup">復元元のバックアップ情報</param>
        private static void RestoreContext(GameContext context, MoveInfo backup)
        {
            context.Black = backup.PrevBlack;
            context.White = backup.PrevWhite;
            context.Turn = backup.PrevTurn;
            context.TurnCount = backup.PrevTurnCount;
            context.Stage = backup.PrevStage;
            context.Move = backup.PrevMove;
            context.Mobility = backup.PrevMobility;
        }

        /// <summary>
        /// Move Ordering を行うかどうかを判定します。
        /// 既存の CachedNegaMax と同じ条件（depth <= 6 相当）を使用します。
        /// </summary>
        /// <param name="remainingDepth">残り探索深さ</param>
        /// <returns>ソートすべき場合は true</returns>
        private bool ShouldOrder(int remainingDepth)
        {
            // 既存: depth <= 6 でソート
            // depth = _currentDepth - remainingDepth
            // depth <= 6 ⇔ _currentDepth - remainingDepth <= 6
            //            ⇔ remainingDepth >= _currentDepth - 6
            return remainingDepth >= _currentDepth - 6;
        }

        /// <summary>
        /// 指し手リストを評価値順に in-place でソートします。
        /// LINQ を使用せず、stackalloc + 挿入ソートによりアロケーションをゼロにします。
        /// MakeMove/UnmakeMove パターンにより盤面を in-place で変更・復元します。
        /// オセロの合法手数は最大でも約 30 手程度であり、挿入ソートで十分な性能が得られます。
        /// </summary>
        /// <param name="moves">指し手リスト（in-place でソートされる）</param>
        /// <param name="context">現在のゲーム状態</param>
        private void OrderMoves(List<int> moves, GameContext context)
        {
            // 評価値を格納する配列（スタック上に確保）
            int count = moves.Count;
            Span<long> scores = stackalloc long[count];

            for (int i = 0; i < count; i++)
            {
                var moveInfo = MakeMove(context, moves[i]);
                try
                {
                    // パスして元の手番から評価（既存と同じ）
                    // Note: BoardAccessor.Pass は Turn のみ変更する。
                    //       UnmakeMove は MakeMove 直前の全フィールド（Turn 含む）を復元するため、
                    //       Pass による Turn 変更も含めて正しく復元される。
                    BoardAccessor.Pass(context);
                    scores[i] = Evaluate(context);
                }
                finally
                {
                    UnmakeMove(context, moveInfo);
                }
            }

            // moves リスト自体を in-place で挿入ソート（降順）
            for (int i = 1; i < count; i++)
            {
                long keyScore = scores[i];
                int keyMove = moves[i];
                int j = i - 1;
                while (j >= 0 && scores[j] < keyScore)
                {
                    scores[j + 1] = scores[j];
                    moves[j + 1] = moves[j];
                    j--;
                }
                scores[j + 1] = keyScore;
                moves[j + 1] = keyMove;
            }
        }
    }
}
