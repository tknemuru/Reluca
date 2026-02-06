/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の Aspiration Window チューニング（ステージ別テーブル・指数拡張）テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - AspirationUseStageTable = false 時に既存動作と同一であること（後方互換性）
/// - AspirationUseStageTable = true 時にステージ別 delta が使用されること
/// - 指数拡張戦略が AspirationUseStageTable = true 時のみ動作すること
/// - ExpandDelta の指数拡張と固定 2 倍拡張の動作検証
/// - ClampDelta のオーバーフロー安全性の検証
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
    /// PvsSearchEngine の Aspiration Window チューニングテストクラスです。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PvsSearchEngineAspirationTuningUnitTest
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
        /// StageTable OFF 時の非干渉: Aspiration OFF と ON（StageTable OFF）で結果一致
        /// </summary>
        [TestMethod]
        public void StageTable_OFF時にAspiration_OFFとONで結果一致()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: Aspiration ON, StageTable OFF（従来動作）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: true,
                aspirationUseStageTable: false);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"StageTable OFF: BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// StageTable ON 時に有効な手を返すこと
        /// </summary>
        [TestMethod]
        public void StageTable_ON時に有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: true,
                aspirationUseStageTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Console.WriteLine($"StageTable ON: BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}");
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"StageTable ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// StageTable ON + TT ON 時に有効な手を返すこと
        /// </summary>
        [TestMethod]
        public void StageTable_ON_TT_ON時に有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true, useAspirationWindow: true,
                aspirationUseStageTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Console.WriteLine($"StageTable ON + TT ON: BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}");
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"StageTable ON + TT ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// StageTable ON と OFF で Aspiration OFF と同じ評価値が返ること（正しさ担保）
        /// </summary>
        [TestMethod]
        public void StageTable_ONでもAspiration_OFFと同じ評価値が返る()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF（基準）
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: StageTable ON
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: true,
                aspirationUseStageTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"StageTable ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// 中盤局面で StageTable ON 時に正しい結果を返す
        /// </summary>
        [TestMethod]
        public void 中盤局面でStageTable_ON時に正しい結果を返す()
        {
            // Arrange
            var context = CreateGameContext(2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: Aspiration OFF（基準）
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: StageTable ON
            var contextOn = CreateGameContext(2, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: false, useAspirationWindow: true,
                aspirationUseStageTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"中盤 Aspiration OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}");
            Console.WriteLine($"中盤 StageTable ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }

        /// <summary>
        /// StageTable ON + MPC ON で有効な手を返す
        /// </summary>
        [TestMethod]
        public void StageTable_ON_MPC_ONで有効な手を返す()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true, useAspirationWindow: true,
                aspirationUseStageTable: true, useMultiProbCut: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Console.WriteLine($"StageTable ON + MPC ON: BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}");
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"StageTable ON + MPC ON: 無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// ExpandDelta の指数拡張: retry 0 で delta * 2
        /// </summary>
        [TestMethod]
        public void ExpandDelta_指数拡張_retry0でdelta2倍()
        {
            // Assert
            Assert.AreEqual(100, PvsSearchEngine.ExpandDelta(50, 0, true));
            Assert.AreEqual(160, PvsSearchEngine.ExpandDelta(80, 0, true));
        }

        /// <summary>
        /// ExpandDelta の指数拡張: retry 1 で delta * 4
        /// </summary>
        [TestMethod]
        public void ExpandDelta_指数拡張_retry1でdelta4倍()
        {
            // Assert
            Assert.AreEqual(200, PvsSearchEngine.ExpandDelta(50, 1, true));
            Assert.AreEqual(320, PvsSearchEngine.ExpandDelta(80, 1, true));
        }

        /// <summary>
        /// ExpandDelta の指数拡張: retry 2 で delta * 8
        /// </summary>
        [TestMethod]
        public void ExpandDelta_指数拡張_retry2でdelta8倍()
        {
            // Assert
            Assert.AreEqual(400, PvsSearchEngine.ExpandDelta(50, 2, true));
            Assert.AreEqual(640, PvsSearchEngine.ExpandDelta(80, 2, true));
        }

        /// <summary>
        /// ExpandDelta の固定 2 倍拡張: 常に delta * 2
        /// </summary>
        [TestMethod]
        public void ExpandDelta_固定2倍拡張_常にdelta2倍()
        {
            // Assert: retryCount に関わらず常に 2 倍
            Assert.AreEqual(100, PvsSearchEngine.ExpandDelta(50, 0, false));
            Assert.AreEqual(100, PvsSearchEngine.ExpandDelta(50, 1, false));
            Assert.AreEqual(100, PvsSearchEngine.ExpandDelta(50, 2, false));
            Assert.AreEqual(160, PvsSearchEngine.ExpandDelta(80, 0, false));
        }

        /// <summary>
        /// ClampDelta のオーバーフロー安全性: 大きな delta と factor で MaxDelta を超えないこと
        /// </summary>
        [TestMethod]
        public void ClampDelta_オーバーフロー安全性()
        {
            // Arrange: MaxDelta = DefaultBeta - DefaultAlpha = 2000000000000000002
            long maxDelta = 2000000000000000002L;

            // Assert: 大きな値でもオーバーフローしない
            long result = PvsSearchEngine.ClampDelta(long.MaxValue / 2, 4);
            Assert.IsTrue(result <= maxDelta,
                $"ClampDelta がオーバーフローしました: {result}");

            // Assert: 通常の値は正しくクランプされる
            Assert.AreEqual(200, PvsSearchEngine.ClampDelta(50, 4));
            Assert.AreEqual(400, PvsSearchEngine.ClampDelta(50, 8));
        }

        /// <summary>
        /// ExpandDelta の固定 2 倍拡張パスにおけるオーバーフロー安全性:
        /// delta が極端に大きい場合でもオーバーフローせず MaxDelta 以下の値を返すこと
        /// </summary>
        [TestMethod]
        public void ExpandDelta_固定2倍拡張_オーバーフロー安全性()
        {
            // Arrange: MaxDelta = DefaultBeta - DefaultAlpha = 2000000000000000002
            long maxDelta = 2000000000000000002L;

            // Assert: delta * 2 が long.MaxValue を超えうる値でもオーバーフローしない
            long result = PvsSearchEngine.ExpandDelta(long.MaxValue / 2, 0, false);
            Assert.IsTrue(result <= maxDelta,
                $"ExpandDelta（固定 2 倍）がオーバーフローしました: {result}");

            // Assert: MaxDelta 付近の delta でもオーバーフローしない
            long resultNearMax = PvsSearchEngine.ExpandDelta(maxDelta, 0, false);
            Assert.IsTrue(resultNearMax <= maxDelta,
                $"ExpandDelta（固定 2 倍、MaxDelta 付近）がオーバーフローしました: {resultNearMax}");
        }
    }
}
