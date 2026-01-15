/// <summary>
/// 【ModuleDoc】
/// 責務: PVS（Principal Variation Search / NegaScout）アルゴリズムによる探索を提供する
/// 入出力: GameContext + SearchOptions + IEvaluable → SearchResult
/// 副作用: なし（Cacher は使用しない）
///
/// 設計方針:
/// - Template Method を使用せず、明示的な制御フローで PVS を実装
/// - Task 3a では TT / 反復深化 / MPC は一切使用しない
/// - LegacySearchEngine と同一の探索結果を保証する
/// </summary>
using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Models;
using Reluca.Updaters;

namespace Reluca.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PVS（Principal Variation Search）アルゴリズムによる探索エンジンです。
    /// NegaScout とも呼ばれ、最初の手をフルウィンドウで探索し、
    /// 2手目以降は Null Window Search で枝刈りを試みます。
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
        /// 着手可能数分析機能
        /// </summary>
        private readonly MobilityAnalyzer _mobilityAnalyzer;

        /// <summary>
        /// 指し手による石の裏返し更新機能
        /// </summary>
        private readonly MoveAndReverseUpdater _reverseUpdater;

        /// <summary>
        /// 探索中の最善手
        /// </summary>
        private int _bestMove;

        /// <summary>
        /// 探索時の最大深さ（Move Ordering 判定用）
        /// </summary>
        private int _maxDepth;

        /// <summary>
        /// 探索時の評価関数
        /// </summary>
        private IEvaluable? _evaluator;

        /// <summary>
        /// コンストラクタ。DI から依存を取得します。
        /// </summary>
        public PvsSearchEngine()
        {
            _mobilityAnalyzer = DiProvider.Get().GetService<MobilityAnalyzer>();
            _reverseUpdater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
            _bestMove = -1;
        }

        /// <summary>
        /// 指定されたゲーム状態から最善手を探索します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="options">探索オプション</param>
        /// <param name="evaluator">評価関数</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
        {
            _bestMove = -1;
            _maxDepth = options.MaxDepth;
            _evaluator = evaluator;

            // PVS 探索を実行
            // 既存の NegaMax は depth=1 から開始し depth >= maxDepth で評価する
            // つまり maxDepth-1 レベル探索後に評価
            // remainingDepth 方式では remainingDepth = maxDepth - 1 から開始し 0 で評価
            var value = Pvs(context, _maxDepth - 1, DefaultAlpha, DefaultBeta, false);

            return new SearchResult(_bestMove, value);
        }

        /// <summary>
        /// PVS（Principal Variation Search）の再帰探索メソッドです。
        /// Task 3a では既存 NegaMax と同一結果を保証するため、
        /// まず標準的な NegaMax として実装しています。
        /// </summary>
        /// <param name="context">現在のゲーム状態</param>
        /// <param name="remainingDepth">残り探索深さ（0 で評価）</param>
        /// <param name="alpha">アルファ値（下界）</param>
        /// <param name="beta">ベータ値（上界）</param>
        /// <param name="isPassed">直前がパスだったかどうか</param>
        /// <returns>評価値</returns>
        private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
        {
            // 終了条件: 残り深さ 0 または終局
            if (remainingDepth == 0 || BoardAccessor.IsGameEndTurnCount(context))
            {
                return Evaluate(context);
            }

            // 合法手を取得
            var moves = _mobilityAnalyzer.Analyze(context);

            long maxValue = DefaultAlpha;

            if (moves.Count > 0)
            {
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
                        UpdateBestMove(move, remainingDepth);
                        return score;
                    }

                    // より良い手が見つかった
                    if (score > maxValue)
                    {
                        maxValue = score;
                        UpdateBestMove(move, remainingDepth);

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

            return maxValue;
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
            // depth = _maxDepth - remainingDepth（remainingDepth が maxDepth-1 から開始するため）
            // depth <= 6 ⇔ _maxDepth - remainingDepth <= 6
            //            ⇔ remainingDepth >= _maxDepth - 6
            return remainingDepth >= _maxDepth - 6;
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

        /// <summary>
        /// 最善手を更新します（ルートノードのみ）。
        /// </summary>
        /// <param name="move">指し手</param>
        /// <param name="remainingDepth">残り探索深さ</param>
        private void UpdateBestMove(int move, int remainingDepth)
        {
            // ルートノード（remainingDepth == _maxDepth - 1）でのみ更新
            if (remainingDepth == _maxDepth - 1)
            {
                _bestMove = move;
            }
        }
    }
}
