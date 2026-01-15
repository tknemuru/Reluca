/// <summary>
/// 【ModuleDoc】
/// 責務: 既存の CachedNegaMax をラップし、ISearchEngine として提供する
/// 入出力: GameContext + SearchOptions + IEvaluable → SearchResult
/// 副作用: 内部で CachedNegaMax を生成し、探索中にキャッシュを使用
/// </summary>
using Reluca.Contexts;
using Reluca.Evaluates;
using Reluca.Serchers;

namespace Reluca.Search
{
    /// <summary>
    /// 既存の CachedNegaMax を ISearchEngine としてラップする探索エンジンです。
    /// 後方互換性のために提供されます。
    /// </summary>
    public class LegacySearchEngine : ISearchEngine
    {
        /// <summary>
        /// 指定されたゲーム状態から最善手を探索します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="options">探索オプション</param>
        /// <param name="evaluator">評価関数</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
        {
            // 探索ごとに新規インスタンスを生成（状態の分離）
            var searcher = CreateSearcher();

            // 探索パラメータを設定
            searcher.Initialize(evaluator, options.MaxDepth);

            // 探索実行
            var bestMove = searcher.Search(context);
            var value = searcher.Value;

            return new SearchResult(bestMove, value);
        }

        /// <summary>
        /// CachedNegaMax インスタンスを生成します。
        /// </summary>
        /// <returns>新規の CachedNegaMax インスタンス</returns>
        private static CachedNegaMax CreateSearcher()
        {
            return new CachedNegaMax();
        }
    }
}
