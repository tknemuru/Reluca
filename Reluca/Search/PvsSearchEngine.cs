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
/// - fail-low/high 時は δ を2倍にして再探索（最大 AspirationMaxRetry 回）
/// - 再探索回数超過時はフルウィンドウにフォールバック（正しさ担保）
/// - fail-low/high 判定は初期窓境界（initialAlpha/initialBeta）で行う
/// - δ 拡大時はオーバーフロー防止のため MaxDelta で clamp
/// - retry 中は TT Store を抑制し、最終確定時のみ Store を許可（TT Clear 不要）
/// </summary>
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
        /// コンストラクタ。DI から依存を注入します。
        /// </summary>
        /// <param name="mobilityAnalyzer">着手可能数分析機能</param>
        /// <param name="reverseUpdater">石の裏返し更新機能</param>
        /// <param name="transpositionTable">置換表</param>
        /// <param name="zobristHash">Zobrist ハッシュ計算機能</param>
        public PvsSearchEngine(
            MobilityAnalyzer mobilityAnalyzer,
            MoveAndReverseUpdater reverseUpdater,
            ITranspositionTable transpositionTable,
            IZobristHash zobristHash)
        {
            _mobilityAnalyzer = mobilityAnalyzer;
            _reverseUpdater = reverseUpdater;
            _transpositionTable = transpositionTable;
            _zobristHash = zobristHash;
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

            // TT 使用時は探索開始前に1回だけクリア（反復ごとの Clear は禁止）
            if (options.UseTranspositionTable)
            {
                _transpositionTable.Clear();
            }

            // 反復深化: depth=1 から MaxDepth まで
            SearchResult result = new SearchResult(-1, 0);
            long prevValue = 0;
            long totalNodesSearched = 0;

            for (int depth = 1; depth <= options.MaxDepth; depth++)
            {
                _currentDepth = depth;
                _nodesSearched = 0;

                // Aspiration Window: depth >= 2 かつ UseAspirationWindow=true の場合
                if (depth >= 2 && options.UseAspirationWindow)
                {
                    result = AspirationRootSearch(context, depth, prevValue);
                }
                else
                {
                    // depth=1 またはAspiration OFF: フルウィンドウで探索
                    result = RootSearch(context, depth, DefaultAlpha, DefaultBeta);
                }

                totalNodesSearched += _nodesSearched;
                Console.WriteLine($"depth={depth} nodes={_nodesSearched} total={totalNodesSearched} value={result.Value}");

                prevValue = result.Value;
            }

            return new SearchResult(result.BestMove, result.Value, totalNodesSearched);
        }

        /// <summary>
        /// Aspiration Window を使用したルート探索を行います。
        /// 前回反復の評価値を中心に狭い窓で探索し、fail-low/high 時は窓を広げて再探索します。
        /// retry 中は TT への書き込みを抑制し、最終確定時のみ Store を許可します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="prevValue">前回反復の評価値</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        private SearchResult AspirationRootSearch(GameContext context, int depth, long prevValue)
        {
            long delta = _options!.AspirationDelta;
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
                if (result.Value <= initialAlpha)
                {
                    // fail-low: 窓が低すぎた → δ を拡大して再探索
                    delta = Math.Min(delta * 2, MaxDelta);
                    retryCount++;
                }
                else if (result.Value >= initialBeta)
                {
                    // fail-high: 窓が高すぎた → δ を拡大して再探索
                    delta = Math.Min(delta * 2, MaxDelta);
                    retryCount++;
                }
                else
                {
                    // 成功: 窓内に収まった → TT Store を許可して再探索
                    _suppressTTStore = false;
                    return RootSearch(context, depth, alpha, beta);
                }
            }

            // 再探索回数超過: フルウィンドウにフォールバック（正しさ担保）
            // TT Store を許可
            _suppressTTStore = false;
            return RootSearch(context, depth, DefaultAlpha, DefaultBeta);
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
