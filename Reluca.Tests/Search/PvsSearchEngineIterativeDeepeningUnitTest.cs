/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の反復深化（Iterative Deepening）テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - ID が最終深さの結果を返すこと
/// - TT ON/OFF で探索結果が一致すること
/// - 各深さで有効な手を返すこと
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
    /// PvsSearchEngine の反復深化（Iterative Deepening）テストクラスです。
    /// </summary>
    [TestClass]
    public class PvsSearchEngineIterativeDeepeningUnitTest
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
            return UnitTestHelper.CreateGameContext("LegacySearchEngine", index, childIndex, type);
        }

        /// <summary>
        /// ID + TT OFF で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void ID_TT_OFFで有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(7, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"ID TT OFF: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// ID + TT ON で探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void ID_TT_ONで有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(7, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"ID TT ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// ID + TT ON と OFF で同一の結果を返す（初期局面）
        /// </summary>
        [TestMethod]
        public void ID_TT_ON_OFFで同一の結果を返す_初期局面()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: TT OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: TT ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"ID TT OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"ID TT ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// ID + TT ON と OFF で同一の結果を返す（中盤局面）
        /// </summary>
        [TestMethod]
        public void ID_TT_ON_OFFで同一の結果を返す_中盤局面()
        {
            // Arrange
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: TT OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: TT ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(2, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"ID TT OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"ID TT ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// ID で深さ 3 の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void ID_深さ3で有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(3, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// ID で深さ 5 の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void ID_深さ5で有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// ID + TT ON で終盤局面の探索が正常に動作する
        /// </summary>
        [TestMethod]
        public void ID_TT_ONで終盤局面の探索が正常に動作する()
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
