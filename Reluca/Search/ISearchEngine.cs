/// <summary>
/// 【ModuleDoc】
/// 責務: 探索エンジンの抽象インターフェース
/// 入出力: GameContext + SearchOptions + IEvaluable → SearchResult
/// 副作用: 探索中にキャッシュを使用する実装あり
/// </summary>
using Reluca.Contexts;
using Reluca.Evaluates;

namespace Reluca.Search
{
    /// <summary>
    /// 探索エンジンのインターフェースを定義します。
    /// </summary>
    public interface ISearchEngine
    {
        /// <summary>
        /// 指定されたゲーム状態から最善手を探索します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="options">探索オプション</param>
        /// <param name="evaluator">評価関数</param>
        /// <returns>探索結果（最善手と評価値）</returns>
        SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator);
    }
}
