/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の Aspiration Window テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - Aspiration ON/OFF で最終結果（BestMove/Value）が一致すること
/// - TT ON/OFF の両方で安定動作すること
/// - 極小δでもフル窓フォールバックにより正しく収束すること
/// </summary>
using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Search;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// PvsSearchEngine の Aspiration Window テストクラスです。
    /// </summary>
    [TestClass]
    public class PvsSearchEngineAspirationWindowUnitTest
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
        /// Aspiration OFF と ON で結果一致（TT OFF）
        /// </summary>
        [TestMethod]
        public void Aspiration_OFFとONで結果一致_TT_OFF()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: Aspiration ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"Aspiration ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// Aspiration OFF と ON で結果一致（TT ON）
        /// </summary>
        [TestMethod]
        public void Aspiration_OFFとONで結果一致_TT_ON()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: true, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: Aspiration ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: true, useAspirationWindow: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"TT ON, Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"TT ON, Aspiration ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// 極小δでもフル窓フォールバックにより正しく収束する
        /// </summary>
        [TestMethod]
        public void 極小δでも正しく収束する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF（基準）
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: Aspiration ON with 極小δ（fail-low/high を強制）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: true,
                aspirationDelta: 1, aspirationMaxRetry: 3);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"Aspiration OFF:   BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"Aspiration δ=1:   BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, δ=1={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, δ=1={resultOn.Value}");
        }

        /// <summary>
        /// 中盤局面でも Aspiration ON/OFF で結果一致
        /// </summary>
        [TestMethod]
        public void 中盤局面でも結果一致()
        {
            // Arrange
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: Aspiration ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(2, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(7, useTranspositionTable: false, useAspirationWindow: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"中盤 Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"中盤 Aspiration ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// TT ON + Aspiration ON で有効な手を返す
        /// </summary>
        [TestMethod]
        public void TT_ON_Aspiration_ONで有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(7, useTranspositionTable: true, useAspirationWindow: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"TT ON + Aspiration ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }
    }
}
