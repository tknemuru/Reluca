/// <summary>
/// 【ModuleDoc】
/// 責務: LegacySearchEngine の回帰テストを提供する
/// 入出力: テストリソース → テスト結果
/// 副作用: なし
/// </summary>
using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Movers;
using Reluca.Search;
using Reluca.Serchers;
using Microsoft.Extensions.DependencyInjection;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// LegacySearchEngine の回帰テストクラスです。
    /// </summary>
    [TestClass]
    public class LegacySearchEngineUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private LegacySearchEngine Target { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public LegacySearchEngineUnitTest()
        {
            Target = new LegacySearchEngine();
        }

        /// <summary>
        /// リソースファイルからゲーム状態を作成します。
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <returns>ゲーム状態</returns>
        private GameContext CreateGameContext(int index, int childIndex, ResourceType type)
        {
            return UnitTestHelper.CreateGameContext("LegacySearchEngine", index, childIndex, type);
        }

        /// <summary>
        /// 固定局面で既存のCachedNegaMaxと同一のBestMoveを返す
        /// </summary>
        [TestMethod]
        public void 固定局面で既存探索と同一のBestMoveを返す()
        {
            // Arrange: 初期局面（黒番）
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(7);

            // 既存の CachedNegaMax で探索
            var legacySearcher = new CachedNegaMax();
            legacySearcher.Initialize(evaluator, 7);
            var expectedMove = legacySearcher.Search(context);
            var expectedValue = legacySearcher.Value;

            // Act: LegacySearchEngine で探索（新規インスタンスで再度探索）
            var contextCopy = BoardAccessor.DeepCopy(context);
            var result = Target.Search(contextCopy, options, evaluator);

            // Assert
            Assert.AreEqual(expectedMove, result.BestMove, "BestMove が一致しません");
            Assert.AreEqual(expectedValue, result.Value, "Value が一致しません");
        }

        /// <summary>
        /// 異なる局面でも既存探索と同一のBestMoveを返す
        /// </summary>
        [TestMethod]
        public void 異なる局面でも既存探索と同一のBestMoveを返す()
        {
            // Arrange: 中盤局面（白番）
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(7);

            // 既存の CachedNegaMax で探索
            var legacySearcher = new CachedNegaMax();
            legacySearcher.Initialize(evaluator, 7);
            var expectedMove = legacySearcher.Search(context);
            var expectedValue = legacySearcher.Value;

            // Act: LegacySearchEngine で探索
            var contextCopy = BoardAccessor.DeepCopy(context);
            var result = Target.Search(contextCopy, options, evaluator);

            // Assert
            Assert.AreEqual(expectedMove, result.BestMove, "BestMove が一致しません");
            Assert.AreEqual(expectedValue, result.Value, "Value が一致しません");
        }

        /// <summary>
        /// FindBestMover経由で終盤局面の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void FindBestMover経由で終盤探索が正常に動作する()
        {
            // Arrange: 終盤局面を作成（TurnCount >= 46）
            var context = CreateGameContext(1, 1, ResourceType.In);
            context.TurnCount = 50; // 終盤として扱う
            context.Stage = 13; // TurnCount 50 に対応する Stage（(50+4)/4 = 13）

            // Act: FindBestMover 経由で探索（ISearchEngine を使用）
            var mover = DiProvider.Get().GetService<FindBestMover>();
            var bestMove = mover.Move(context);

            // Assert: 有効な手が返される（-1 以外）
            Assert.IsTrue(bestMove >= 0 && bestMove < 64, $"無効な手が返されました: {bestMove}");
        }

        /// <summary>
        /// ISearchEngineがDIから正しく解決される
        /// </summary>
        [TestMethod]
        public void ISearchEngineがDIから正しく解決される()
        {
            // Act
            var engine = DiProvider.Get().GetService<ISearchEngine>();

            // Assert
            Assert.IsNotNull(engine, "ISearchEngine が null です");
            Assert.IsInstanceOfType(engine, typeof(LegacySearchEngine), "LegacySearchEngine が解決されていません");
        }

        /// <summary>
        /// 探索ごとに新規インスタンスが生成される（Transient確認）
        /// </summary>
        [TestMethod]
        public void 探索ごとに新規インスタンスが生成される()
        {
            // Act
            var engine1 = DiProvider.Get().GetService<ISearchEngine>();
            var engine2 = DiProvider.Get().GetService<ISearchEngine>();

            // Assert: Transient なので異なるインスタンス
            Assert.AreNotSame(engine1, engine2, "同一インスタンスが返されました（Transient ではありません）");
        }
    }
}
