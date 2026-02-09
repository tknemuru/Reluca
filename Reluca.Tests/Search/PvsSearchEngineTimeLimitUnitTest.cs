/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の時間制御機能の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - TimeLimitMs = null 時に従来動作と同一であることを確認
/// - TimeLimitMs を十分に大きく設定した場合に MaxDepth まで完了することを確認
/// - TimeLimitMs を短く設定した場合に制限時間付近で打ち切られることを確認
/// - TimeLimitMs を極端に短く設定した場合でも最低 1 手が保証されることを確認
/// - CompletedDepth が実際に完了した深さを正しく報告することを確認
/// </summary>
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Search;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PvsSearchEngine の時間制御機能に関する単体テストクラスです。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PvsSearchEngineTimeLimitUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private PvsSearchEngine Target { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PvsSearchEngineTimeLimitUnitTest()
        {
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
        /// TimeLimitMs 未指定時に従来通り MaxDepth まで探索が完了する
        /// </summary>
        [TestMethod]
        public void TimeLimitMs未指定時にMaxDepthまで探索が完了する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, timeLimitMs: null);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: MaxDepth まで完了
            Assert.AreEqual(5, result.CompletedDepth,
                $"CompletedDepth が MaxDepth と一致しません: {result.CompletedDepth}");
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Assert.IsTrue(result.ElapsedMs >= 0,
                $"ElapsedMs が負値です: {result.ElapsedMs}");
        }

        /// <summary>
        /// TimeLimitMs を十分に大きく設定した場合に MaxDepth まで探索が完了する
        /// </summary>
        [TestMethod]
        public void TimeLimitMsが十分に大きい場合にMaxDepthまで探索が完了する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            // 60 秒の制限時間（MaxDepth=5 の探索には十分すぎる）
            var options = new SearchOptions(5, timeLimitMs: 60000);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: MaxDepth まで完了
            Assert.AreEqual(5, result.CompletedDepth,
                $"CompletedDepth が MaxDepth と一致しません: {result.CompletedDepth}");
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// TimeLimitMs を極端に短く設定した場合でも最低 depth=1 の結果が保証される
        /// </summary>
        [TestMethod]
        public void TimeLimitMsが極端に短くても最低1手が返される()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            // 1ms の制限時間（極端に短い）
            var options = new SearchOptions(20, timeLimitMs: 1);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される（depth=1 の結果が保証される）
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Assert.IsTrue(result.CompletedDepth >= 1,
                $"CompletedDepth が 1 未満です: {result.CompletedDepth}");
        }

        /// <summary>
        /// 時間制限で打ち切られた場合に CompletedDepth が MaxDepth より小さい
        /// </summary>
        [TestMethod]
        public void 時間制限による打ち切りでCompletedDepthがMaxDepthより小さくなる()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            // 非常に短い制限時間で深い MaxDepth を設定
            var options = new SearchOptions(20, timeLimitMs: 50);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: 制限時間により打ち切られ、CompletedDepth < MaxDepth
            Assert.IsTrue(result.CompletedDepth < 20,
                $"CompletedDepth が MaxDepth と同じです（打ち切りが発生していません）: {result.CompletedDepth}");
            Assert.IsTrue(result.CompletedDepth >= 1,
                $"CompletedDepth が 1 未満です: {result.CompletedDepth}");
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// 制限時間の遵守を確認する（実際の経過時間が制限時間の 1.1 倍以内）
        /// </summary>
        [TestMethod]
        public void 制限時間が概ね遵守される()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            long timeLimitMs = 500;
            var options = new SearchOptions(20, timeLimitMs: timeLimitMs);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: 経過時間が制限時間の 1.1 倍 + 100ms 以内
            // （レイヤー 2 のチェック間隔と depth=1 の保証分を考慮）
            long maxAllowedMs = (long)(timeLimitMs * 1.1) + 100;
            Console.WriteLine($"TimeLimitMs={timeLimitMs}, ElapsedMs={result.ElapsedMs}, CompletedDepth={result.CompletedDepth}");
            Assert.IsTrue(result.ElapsedMs <= maxAllowedMs,
                $"経過時間が制限時間を大幅に超過しています: ElapsedMs={result.ElapsedMs}, MaxAllowed={maxAllowedMs}");
        }

        /// <summary>
        /// TimeLimitMs 未指定時の BestMove と Value が従来と一致する（非干渉テスト）
        /// </summary>
        [TestMethod]
        public void TimeLimitMs未指定時に従来の探索結果と一致する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var optionsWithoutTimeLimit = new SearchOptions(5, timeLimitMs: null);

            // 比較用: 別インスタンスで従来動作を実行
            var target2 = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsBaseline = new SearchOptions(5);

            // Act
            var result1 = Target.Search(context, options: optionsWithoutTimeLimit, evaluator);
            var result2 = target2.Search(context, options: optionsBaseline, evaluator);

            // Assert: BestMove と Value が一致
            Assert.AreEqual(result2.BestMove, result1.BestMove,
                $"BestMove が一致しません: TimeLimitMs=null → {result1.BestMove}, 従来 → {result2.BestMove}");
            Assert.AreEqual(result2.Value, result1.Value,
                $"Value が一致しません: TimeLimitMs=null → {result1.Value}, 従来 → {result2.Value}");
        }

        /// <summary>
        /// MPC + 時間制御の組み合わせで正しく動作する
        /// </summary>
        [TestMethod]
        public void MPCと時間制御の組み合わせで正しく動作する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(
                20,
                useTranspositionTable: true,
                useMultiProbCut: true,
                timeLimitMs: 500);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Assert.IsTrue(result.CompletedDepth >= 1,
                $"CompletedDepth が 1 未満です: {result.CompletedDepth}");
            Console.WriteLine($"MPC+TimeLimit: CompletedDepth={result.CompletedDepth}, ElapsedMs={result.ElapsedMs}");
        }

        /// <summary>
        /// レイヤー 2 タイムアウト発生時に中断された深さのノード数も NodesSearched に含まれる
        /// </summary>
        [TestMethod]
        public void タイムアウト発生時に中断された深さのノード数もNodesSearchedに含まれる()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            // 短い制限時間で深い MaxDepth を設定し、タイムアウトを発生させる
            var options = new SearchOptions(20, timeLimitMs: 50);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: NodesSearched が 0 より大きい（中断された深さのノードも含まれている）
            Assert.IsTrue(result.NodesSearched > 0,
                $"NodesSearched が 0 です: {result.NodesSearched}");
            Console.WriteLine($"タイムアウト時 NodesSearched={result.NodesSearched}, CompletedDepth={result.CompletedDepth}");
        }

        /// <summary>
        /// TimeLimitMs に負値を指定した場合に ArgumentOutOfRangeException がスローされる
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TimeLimitMsに負値を指定するとArgumentOutOfRangeExceptionがスローされる()
        {
            // Arrange & Act
            var options = new SearchOptions(5, timeLimitMs: -100);

            // Assert: ExpectedException 属性により例外が期待される
        }

        /// <summary>
        /// Aspiration Window + 時間制御の組み合わせで正しく動作する
        /// </summary>
        [TestMethod]
        public void AspirationWindowと時間制御の組み合わせで正しく動作する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(
                20,
                useTranspositionTable: true,
                useAspirationWindow: true,
                aspirationUseStageTable: true,
                useMultiProbCut: true,
                timeLimitMs: 500);

            // Act
            var result = Target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Assert.IsTrue(result.CompletedDepth >= 1,
                $"CompletedDepth が 1 未満です: {result.CompletedDepth}");
            Console.WriteLine($"Aspiration+TimeLimit: CompletedDepth={result.CompletedDepth}, ElapsedMs={result.ElapsedMs}");
        }
    }
}
