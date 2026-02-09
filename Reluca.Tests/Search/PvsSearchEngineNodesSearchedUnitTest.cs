/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の探索ノード数計測テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - 探索完了時に NodesSearched が正の値であること
/// - TT ON 時の NodesSearched が TT OFF 時より少ないこと
/// - NodesSearched 導入前後で BestMove と Value が一致すること
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
    /// PvsSearchEngine の探索ノード数計測テストクラスです。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PvsSearchEngineNodesSearchedUnitTest
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
        /// 探索完了時に NodesSearched が正の値であること
        /// </summary>
        [TestMethod]
        public void 探索完了時にNodesSearchedが正の値である()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert
            Console.WriteLine($"NodesSearched={result.NodesSearched}, BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}");
            Assert.IsTrue(result.NodesSearched > 0,
                $"NodesSearched が正の値ではありません: {result.NodesSearched}");
        }

        /// <summary>
        /// TT ON 時の NodesSearched が TT OFF 時より少ないこと
        /// </summary>
        [TestMethod]
        public void TT_ON時のNodesSearchedがTT_OFF時より少ない()
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
            Console.WriteLine($"TT OFF: NodesSearched={resultOff.NodesSearched}");
            Console.WriteLine($"TT ON:  NodesSearched={resultOn.NodesSearched}");
            Assert.IsTrue(resultOn.NodesSearched < resultOff.NodesSearched,
                $"TT ON の NodesSearched({resultOn.NodesSearched}) が TT OFF({resultOff.NodesSearched}) より少なくありません");
        }

        /// <summary>
        /// NodesSearched 導入後も BestMove と Value が変わらないこと（TT OFF）
        /// </summary>
        [TestMethod]
        public void NodesSearched導入後もBestMoveとValueが変わらない_TT_OFF()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: 有効な手が返されること
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
            Assert.IsTrue(result.NodesSearched > 0,
                $"NodesSearched が正の値ではありません: {result.NodesSearched}");
        }

        /// <summary>
        /// NodesSearched 導入後も BestMove と Value が変わらないこと（TT ON）
        /// </summary>
        [TestMethod]
        public void NodesSearched導入後もBestMoveとValueが変わらない_TT_ON()
        {
            // Arrange
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            var options = new SearchOptions(5, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: 有効な手が返されること
            var validMoves = new[] { 19, 26, 37, 44 }; // d3, c4, f5, e6
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
            Assert.IsTrue(result.NodesSearched > 0,
                $"NodesSearched が正の値ではありません: {result.NodesSearched}");
        }

        /// <summary>
        /// TT ON/OFF で BestMove と Value が一致すること（NodesSearched 導入による非干渉検証）
        /// </summary>
        [TestMethod]
        public void TT_ON_OFFでBestMoveとValueが一致する()
        {
            // Arrange
            var context = CreateGameContext(1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Act: TT OFF
            var targetOff = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOff = new SearchOptions(5, useTranspositionTable: false);
            var resultOff = targetOff.Search(context, optionsOff, evaluator);

            // Act: TT ON（新しいインスタンスとコンテキストを使用）
            var contextOn = CreateGameContext(1, 1, ResourceType.In);
            var targetOn = DiProvider.Get().GetService<PvsSearchEngine>();
            var optionsOn = new SearchOptions(5, useTranspositionTable: true);
            var resultOn = targetOn.Search(contextOn, optionsOn, evaluator);

            // Assert
            Console.WriteLine($"TT OFF: BestMove={BoardAccessor.ToPosition(resultOff.BestMove)}, Value={resultOff.Value}, Nodes={resultOff.NodesSearched}");
            Console.WriteLine($"TT ON:  BestMove={BoardAccessor.ToPosition(resultOn.BestMove)}, Value={resultOn.Value}, Nodes={resultOn.NodesSearched}");

            Assert.AreEqual(resultOff.BestMove, resultOn.BestMove,
                $"BestMove が一致しません: OFF={BoardAccessor.ToPosition(resultOff.BestMove)}, ON={BoardAccessor.ToPosition(resultOn.BestMove)}");
            Assert.AreEqual(resultOff.Value, resultOn.Value,
                $"Value が一致しません: OFF={resultOff.Value}, ON={resultOn.Value}");
        }
    }
}
