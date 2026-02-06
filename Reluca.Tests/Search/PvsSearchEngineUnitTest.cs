/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - TT OFF で有効な手が返ることを確認
/// - DI 経由での解決が正しく動作すること
/// </summary>
using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Search;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PvsSearchEngine の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class PvsSearchEngineUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private PvsSearchEngine Target { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PvsSearchEngineUnitTest()
        {
            // DI 経由でインスタンスを取得
            Target = DiProvider.Get().GetService<PvsSearchEngine>();
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
            return UnitTestHelper.CreateGameContext("PvsSearchEngine", index, childIndex, type);
        }

        /// <summary>
        /// 初期局面で探索が正常に動作し、有効な手を返す
        /// </summary>
        [TestMethod]
        public void 初期局面で探索が正常に動作する()
        {
            // Arrange: 初期局面（黒番）
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5);

            // Act: PvsSearchEngine で探索
            var pvsResult = Target.Search(context, options, evaluator);

            // デバッグ出力
            Console.WriteLine($"PVS: BestMove={BoardAccessor.ToPosition(pvsResult.BestMove)}, Value={pvsResult.Value}");

            // Assert: 有効な手が返される（初期局面では 4 つの手がある: d3, c4, f5, e6）
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(pvsResult.BestMove),
                $"無効な手が返されました: {BoardAccessor.ToPosition(pvsResult.BestMove)}");
        }

        /// <summary>
        /// 中盤局面で探索が正常に動作し、有効な手を返す
        /// </summary>
        [TestMethod]
        public void 中盤局面で探索が正常に動作する()
        {
            // Arrange: 中盤局面（白番）
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5);

            // Act: PvsSearchEngine で探索
            var pvsResult = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(pvsResult.BestMove >= 0 && pvsResult.BestMove < 64,
                $"無効な手が返されました: {pvsResult.BestMove}");
        }

        /// <summary>
        /// 終盤局面（DiscCountEvaluator）で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void 終盤局面で探索が正常に動作する()
        {
            // Arrange: 終盤局面を作成
            var context = CreateGameContext(1, 1, ResourceType.In);
            context.TurnCount = 50;

            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            var options = new SearchOptions(5);

            // Act: PvsSearchEngine で探索
            var pvsResult = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(pvsResult.BestMove >= 0 && pvsResult.BestMove < 64,
                $"無効な手が返されました: {pvsResult.BestMove}");
        }

        /// <summary>
        /// 深さ5で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void 深さ5で探索が正常に動作する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5);

            // Act: PvsSearchEngine で探索
            var pvsResult = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(pvsResult.BestMove >= 0 && pvsResult.BestMove < 64,
                $"無効な手が返されました: {pvsResult.BestMove}");
        }

        /// <summary>
        /// PvsSearchEngine が DI から正しく解決される
        /// </summary>
        [TestMethod]
        public void PvsSearchEngineがDIから正しく解決される()
        {
            // Act
            var engine = DiProvider.Get().GetService<PvsSearchEngine>();

            // Assert
            Assert.IsNotNull(engine, "PvsSearchEngine が null です");
        }

        /// <summary>
        /// PvsSearchEngine は Transient として登録されている
        /// </summary>
        [TestMethod]
        public void PvsSearchEngineはTransientとして登録されている()
        {
            // Act
            var engine1 = DiProvider.Get().GetService<PvsSearchEngine>();
            var engine2 = DiProvider.Get().GetService<PvsSearchEngine>();

            // Assert: Transient なので異なるインスタンス
            Assert.AreNotSame(engine1, engine2, "同一インスタンスが返されました（Transient ではありません）");
        }
    }
}
