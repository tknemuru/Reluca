/// <summary>
/// 【ModuleDoc】
/// 責務: 最善手を決定するための入口を提供する
/// 入出力: GameContext → int（最善手のインデックス）
/// 副作用: DI から探索エンジンと評価関数を取得
/// </summary>
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
        /// 終盤時の探索深さ
        /// </summary>
        private const int EndgameDepth = 99;

        /// <summary>
        /// 通常時の探索深さ
        /// </summary>
        private const int NormalDepth = 7;

        /// <summary>
        /// 探索エンジン
        /// </summary>
        private ISearchEngine? SearchEngine { get; set; }

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
            if (context.TurnCount >= EndgameTurnThreshold)
            {
                evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
                depth = EndgameDepth;
            }
            else
            {
                evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
                depth = NormalDepth;
            }

            var options = new SearchOptions(depth);
            var result = SearchEngine.Search(context, options, evaluator);

            return result.BestMove;
        }
    }
}
