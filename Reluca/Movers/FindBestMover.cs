/// <summary>
/// 【ModuleDoc】
/// 責務: 最善手を決定するための入口を提供する
/// 入出力: GameContext → int（最善手のインデックス）
/// 副作用: DI から探索エンジンと評価関数を取得
///
/// 時間制御統合:
/// - RemainingTimeMs が設定されている場合、TimeAllocator により各手番の制限時間を計算する
/// - 終盤完全読み切りモード（TurnCount >= EndgameTurnThreshold）では時間制限を適用しない
/// </summary>
using System.Numerics;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Search;

namespace Reluca.Movers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
#pragma warning disable CS8604
    /// <summary>
    /// 最善手の返却機能を提供します。
    /// </summary>
    public class FindBestMover : IMovable
    {
        /// <summary>
        /// 終盤と判定するターン数の閾値
        /// </summary>
        private const int EndgameTurnThreshold = 46;

        /// <summary>
        /// 通常時の探索深さ
        /// </summary>
        private const int NormalDepth = 7;

        /// <summary>
        /// 探索エンジン
        /// </summary>
        private ISearchEngine? SearchEngine { get; set; }

        /// <summary>
        /// 時間配分器
        /// </summary>
        private TimeAllocator? _timeAllocator;

        /// <summary>
        /// 残り持ち時間（ミリ秒）。外部から設定する。
        /// null の場合は時間制限なしで動作する。
        /// </summary>
        public long? RemainingTimeMs { get; set; }

        /// <summary>
        /// 指し手を決めます。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>指し手</returns>
        public int Move(GameContext context)
        {
            SearchEngine = DiProvider.Get().GetService<ISearchEngine>();

            // 終盤 Evaluator 切替ロジック（従来の挙動を維持）
            IEvaluable evaluator;
            int depth;
            long? timeLimitMs = null;

            if (context.TurnCount >= EndgameTurnThreshold)
            {
                evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
                int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
                depth = emptyCount;
                // 終盤完全読み切りモードでは時間制限を適用しない
                // （Non-Goals に記載の通り、別途 RFC で対応）
            }
            else
            {
                evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
                depth = NormalDepth;

                // 通常探索時のみ時間制限を計算
                if (RemainingTimeMs.HasValue)
                {
                    _timeAllocator ??= new TimeAllocator();
                    timeLimitMs = _timeAllocator.Allocate(
                        RemainingTimeMs.Value, context.TurnCount);
                }
            }

            var options = new SearchOptions(
                depth,
                useTranspositionTable: true,
                useAspirationWindow: true,
                aspirationUseStageTable: true,
                useMultiProbCut: true,
                timeLimitMs: timeLimitMs
            );
            var result = SearchEngine.Search(context, options, evaluator);

            return result.BestMove;
        }
    }
}
