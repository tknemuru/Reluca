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
/// - retry 中は TT Store を抑制し、最終確定時のみ Store を許可（TT Clear 不要）
/// - retry 発生回数とフォールバック回数を計測し、ログ出力する
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
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Evaluates;
using Reluca.Models;
using Reluca.Search.Transposition;
using Reluca.Updaters;

namespace Reluca.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PVS（Principal Variation Search）アルゴリズムによる探索エンジンです。
    /// NegaScout とも呼ばれ、最初の手をフルウィンドウで探索し、
    /// 2手目以降は Null Window Search で枝刈りを試みます。
    /// 反復深化（Iterative Deepening）により効率的な探索を実現します。
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
        /// 指し手による石の裏返し更新機能
        /// </summary>
        private readonly MoveAndReverseUpdater _reverseUpdater;

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

        /// <summary>
        /// 探索の開始時刻を計測するストップウォッチ
        /// </summary>
        private readonly Stopwatch _stopwatch = new();

        /// <summary>
        /// 探索の制限時間（ミリ秒）。null は制限なし
        /// </summary>
        private long? _timeLimitMs;

        /// <summary>
        /// コンストラクタ。DI から依存を注入します。
        /// </summary>
        /// <param name="logger">ロガー</param>
        /// <param name="mobilityAnalyzer">着手可能数分析機能</param>
        /// <param name="reverseUpdater">石の裏返し更新機能</param>
        /// <param name="transpositionTable">置換表</param>
        /// <param name="zobristHash">Zobrist ハッシュ計算機能</param>
        /// <param name="mpcParameterTable">MPC パラメータテーブル</param>
        /// <param name="aspirationParameterTable">Aspiration Window のステージ別パラメータテーブル</param>
        public PvsSearchEngine(
            ILogger<PvsSearchEngine> logger,
            MobilityAnalyzer mobilityAnalyzer,
            MoveAndReverseUpdater reverseUpdater,
            ITranspositionTable transpositionTable,
            IZobristHash zobristHash,
            MpcParameterTable mpcParameterTable,
            AspirationParameterTable aspirationParameterTable)
        {
            _logger = logger;
            _mobilityAnalyzer = mobilityAnalyzer;
            _reverseUpdater = reverseUpdater;
            _transpositionTable = transpositionTable;
            _zobristHash = zobristHash;
            _mpcParameterTable = mpcParameterTable;
            _aspirationParameterTable = aspirationParameterTable;
            _bestMove = -1;
        }

        /// <summary>
        /// 指定されたゲーム状態から最善手を探索します。
        /// 反復深化（Iterative Deepening）により depth=1 から MaxDepth まで順次探索します。
        /// Aspiration Window 有効時は depth >= 2 で狭い窓から探索を開始します。
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
        /// retry 中は TT への書き込みを抑制し、最終確定時のみ Store を許可します。
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

                // retry 中は TT Store を抑制
                _suppressTTStore = true;
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
                    // 成功: 窓内に収まった → TT Store を許可して再探索
                    _suppressTTStore = false;
                    return RootSearch(context, depth, alpha, beta);
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
            if (moves.Count == 0)
            {
                return new SearchResult(-1, Evaluate(context));
            }

            // 手順序の最適化（優先順位: 1) TT bestMove, 2) 前回反復の bestMove, 3) その他）
            moves = OptimizeMoveOrder(moves, context, depth);

            // originalAlpha を保存（BoundType 判定用）
            long originalAlpha = alpha;
            long maxValue = DefaultAlpha;
            int rootBestMove = moves[0]; // 最初の手をデフォルトとする

            // ルートのハッシュを計算（TT Store 用）
            ulong rootHash = 0;
            if (_options?.UseTranspositionTable == true)
            {
                rootHash = _zobristHash.ComputeHash(context);
            }

            foreach (var move in moves)
            {
                var child = MakeMove(context, move);

                // 再帰探索
                // depth=1 の時 remainingDepth=0 で即評価
                // depth=N の時 remainingDepth=N-1 で (N-1) レベル探索
                long score = -Pvs(child, depth - 1, -beta, -alpha, false);

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
        /// <returns>最適化された手順序</returns>
        private List<int> OptimizeMoveOrder(List<int> moves, GameContext context, int depth)
        {
            int? ttMove = null;
            int? prevBestMove = null;

            // 1) TT bestMove を取得
            if (_options?.UseTranspositionTable == true)
            {
                ulong hash = _zobristHash.ComputeHash(context);
                int ttBestMove = _transpositionTable.GetBestMove(hash);
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
                moves = OrderMoves(moves, context);
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
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="remainingDepth">残り探索深さ（0 で評価）</param>
        /// <param name="alpha">アルファ値（下界）</param>
        /// <param name="beta">ベータ値（上界）</param>
        /// <param name="isPassed">直前がパスだったかどうか</param>
        /// <returns>評価値</returns>
        private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
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

            // TT Probe
            ulong hash = 0;
            if (_options?.UseTranspositionTable == true)
            {
                hash = _zobristHash.ComputeHash(context);
                if (_transpositionTable.TryProbe(hash, remainingDepth, alpha, beta, out var entry))
                {
                    return entry.Value;
                }
            }

            // MPC 判定（TT Probe 後・合法手展開前に挿入）
            // パス直後のノードでは MPC を適用しない（局面の性質が大きく変化しているため）
            if (_mpcEnabled && !isPassed)
            {
                var mpcResult = TryMultiProbCut(context, remainingDepth, alpha, beta);
                if (mpcResult.HasValue)
                {
                    return mpcResult.Value;
                }
            }

            // 合法手を取得
            var moves = _mobilityAnalyzer.Analyze(context);

            long maxValue = DefaultAlpha;
            int localBestMove = TTEntry.NoBestMove;

            if (moves.Count > 0)
            {
                // TT の bestMove を先頭に移動（Move Ordering 改善）
                if (_options?.UseTranspositionTable == true)
                {
                    int ttMove = _transpositionTable.GetBestMove(hash);
                    if (ttMove != TTEntry.NoBestMove && moves.Contains(ttMove))
                    {
                        moves.Remove(ttMove);
                        moves.Insert(0, ttMove);
                    }
                }

                // Move Ordering（既存と同じ条件: depth <= 6 相当）
                if (ShouldOrder(remainingDepth))
                {
                    moves = OrderMoves(moves, context);
                }

                foreach (var move in moves)
                {
                    // 着手を実行
                    var child = MakeMove(context, move);

                    // 再帰探索（標準的な NegaMax）
                    long score = -Pvs(child, remainingDepth - 1, -beta, -alpha, false);

                    // ベータカット
                    if (score >= beta)
                    {
                        // TT Store（LowerBound）- Aspiration retry 中は Store を抑制
                        if (_options?.UseTranspositionTable == true && !_suppressTTStore)
                        {
                            _transpositionTable.Store(hash, remainingDepth, score, BoundType.LowerBound, move);
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
                // パス処理（既存と同じ: context を直接変更、depth もカウント）
                BoardAccessor.Pass(context);
                maxValue = -Pvs(context, remainingDepth - 1, -beta, -alpha, true);
            }

            // TT Store - Aspiration retry 中は Store を抑制
            if (_options?.UseTranspositionTable == true && !_suppressTTStore && moves.Count > 0)
            {
                var boundType = DetermineBoundType(maxValue, originalAlpha, beta);
                _transpositionTable.Store(hash, remainingDepth, maxValue, boundType, localBestMove);
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
        /// <returns>カット値。カットしない場合は null</returns>
        private long? TryMultiProbCut(GameContext context, int remainingDepth, long alpha, long beta)
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
                long shallowValue = Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false);
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
        /// 指し手を実行し、次の局面を生成します。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="move">指し手</param>
        /// <returns>着手後のゲーム状態</returns>
        private GameContext MakeMove(GameContext context, int move)
        {
            var copyContext = BoardAccessor.DeepCopy(context);
            copyContext.Move = move;
            _reverseUpdater.Update(copyContext);
            BoardAccessor.NextTurn(copyContext);
            return copyContext;
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
        /// 指し手を評価値順にソートします。
        /// 既存の CachedNegaMax.MoveOrdering と同じロジックを使用します。
        /// </summary>
        /// <param name="moves">指し手リスト</param>
        /// <param name="context">現在のゲーム状態</param>
        /// <returns>ソート済みの指し手リスト</returns>
        private List<int> OrderMoves(List<int> moves, GameContext context)
        {
            // 各手を実行して評価し、評価値の高い順（相手から見て悪い順）にソート
            var ordered = moves
                .OrderByDescending(move =>
                {
                    var child = MakeMove(context, move);
                    // パスして元の手番から評価（既存と同じ）
                    BoardAccessor.Pass(child);
                    return Evaluate(child);
                })
                .ToList();
            return ordered;
        }
    }
}
