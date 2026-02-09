/// <summary>
/// 【ModuleDoc】
/// 責務: PvsSearchEngine の NPS（Nodes Per Second）ベンチマークを提供する
/// 入出力: テストケース → コンソールへの NPS 計測結果出力
/// 副作用: なし
///
/// 備考:
/// - RFC 7.2 節のベンチマーク条件に基づく計測を行う
/// - 3局面 × 2深さ × 5回計測、中央値を採用
/// - ウォームアップ: 計測前に同一条件で2回の探索を実行
/// </summary>
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Models;
using Reluca.Search;
using System.Diagnostics;
using System.Numerics;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602
    /// <summary>
    /// NPS ベンチマークテストクラスです。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class NpsBenchmarkTest
    {
        /// <summary>
        /// 計測回数
        /// </summary>
        private const int MeasureCount = 5;

        /// <summary>
        /// ウォームアップ回数
        /// </summary>
        private const int WarmupCount = 2;

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
        /// 終盤局面（空マス14以下）のゲーム状態を作成します。
        /// ビットボードを直接指定して、50手目前後・空マス14の実際の終盤局面を構築します。
        ///
        /// 盤面配置（黒26石, 白24石, 空14マス）:
        /// 　ａｂｃｄｅｆｇｈ
        /// １○●○○○○●●
        /// ２○●●●○●●●
        /// ３○○●●●●●●
        /// ４○○○●●○○
        /// ５●○●●○○
        /// ６●●●○○
        /// ７●●○○
        /// ８●○○○
        /// </summary>
        /// <returns>終盤局面のゲーム状態</returns>
        private static GameContext CreateEndgameContext()
        {
            // Black: 26石, White: 24石, Empty: 14マス
            ulong black = 0x0103070D18FCEEC2UL;
            ulong white = 0x0E0C18326703113DUL;

            Debug.Assert((black & white) == 0, "黒石と白石が重複しています");
            Debug.Assert(BitOperations.PopCount(black) == 26, "黒石の数が不正です");
            Debug.Assert(BitOperations.PopCount(white) == 24, "白石の数が不正です");
            Debug.Assert(64 - BitOperations.PopCount(black | white) == 14, "空マス数が14ではありません");

            var ctx = new GameContext
            {
                Board = new BoardContext { Black = black, White = white },
                Turn = Disc.Color.Black,
                TurnCount = 50,
                Stage = 13,  // Math.Min((50 + 4) / 4, 15) = 13
            };
            return ctx;
        }

        /// <summary>
        /// 指定条件で NPS を計測します。
        /// </summary>
        /// <param name="positionName">局面名</param>
        /// <param name="contextFactory">ゲーム状態生成関数</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="evaluatorFactory">評価関数生成関数</param>
        private void MeasureNps(string positionName, Func<GameContext> contextFactory, int depth, Func<IEvaluable> evaluatorFactory)
        {
            var options = new SearchOptions(
                depth,
                useTranspositionTable: true,
                useAspirationWindow: true,
                aspirationUseStageTable: true,
                useMultiProbCut: true);

            // ウォームアップ
            for (int i = 0; i < WarmupCount; i++)
            {
                var engine = DiProvider.Get().GetService<PvsSearchEngine>();
                var ctx = contextFactory();
                var eval = evaluatorFactory();
                engine.Search(ctx, options, eval);
            }

            // 計測
            var npsValues = new List<double>();
            for (int i = 0; i < MeasureCount; i++)
            {
                var engine = DiProvider.Get().GetService<PvsSearchEngine>();
                var ctx = contextFactory();
                var eval = evaluatorFactory();

                var sw = Stopwatch.StartNew();
                var result = engine.Search(ctx, options, eval);
                sw.Stop();

                double elapsedSec = sw.Elapsed.TotalSeconds;
                double nps = elapsedSec > 0 ? result.NodesSearched / elapsedSec : 0;
                npsValues.Add(nps);
            }

            // 中央値
            npsValues.Sort();
            double median = npsValues[MeasureCount / 2];

            Console.WriteLine($"BENCHMARK: Position={positionName}, Depth={depth}, MedianNPS={median:F0}, " +
                $"AllNPS=[{string.Join(", ", npsValues.Select(n => n.ToString("F0")))}]");
        }

        /// <summary>
        /// 全条件の NPS ベンチマークを実行します。
        /// </summary>
        [TestMethod]
        public void 全条件NPS計測()
        {
            Console.WriteLine("=== NPS Benchmark Start ===");

            // 初期局面 depth=8
            MeasureNps("初期局面", () => CreateGameContext(1, 1, ResourceType.In), 8,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            // 初期局面 depth=10
            MeasureNps("初期局面", () => CreateGameContext(1, 1, ResourceType.In), 10,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            // 中盤局面 depth=8
            MeasureNps("中盤局面", () => CreateGameContext(2, 1, ResourceType.In), 8,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            // 中盤局面 depth=10
            MeasureNps("中盤局面", () => CreateGameContext(2, 1, ResourceType.In), 10,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            // 終盤局面 depth=8（空マス14の実際の終盤局面）
            MeasureNps("終盤局面", CreateEndgameContext, 8,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            // 終盤局面 depth=10（空マス14の実際の終盤局面）
            MeasureNps("終盤局面", CreateEndgameContext, 10,
                () => DiProvider.Get().GetService<FeaturePatternEvaluator>());

            Console.WriteLine("=== NPS Benchmark Complete ===");
        }
    }
}
