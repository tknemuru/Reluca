/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の Transposition Table 統合テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - TT OFF で有効な手が返ることを確認
/// - TT ON で有効な手が返ることを確認
/// - TT ON/OFF で同一結果が返ることを確認
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
    /// PvsSearchEngine の Transposition Table 統合テストクラスです。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PvsSearchEngineWithTTUnitTest
    {
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
        /// TT OFF で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void TT_OFFで探索が正常に動作する()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"TT OFF: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// TT ON で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void TT_ONで探索が正常に動作する()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"TT ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// TT ON と OFF で同一の BestMove を返す（初期局面）
        /// </summary>
        [TestMethod]
        public void TT_ON_OFFで同一の結果を返す_初期局面()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: TT OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: TT ON（新しいインスタンスを使用）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"TT OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"TT ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// TT ON と OFF で同一の BestMove を返す（中盤局面）
        /// </summary>
        [TestMethod]
        public void TT_ON_OFFで同一の結果を返す_中盤局面()
        {
            // Arrange
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: TT OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: TT ON（新しいインスタンスを使用）
            var contextOn = CreateGameContext(2, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"TT OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"TT ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// TT ON で深さ 5 の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void TT_ONで深さ5の探索が正常に動作する()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// TT ON で終盤局面の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void TT_ONで終盤局面の探索が正常に動作する()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            context.TurnCount = 50;
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }
    }
}
