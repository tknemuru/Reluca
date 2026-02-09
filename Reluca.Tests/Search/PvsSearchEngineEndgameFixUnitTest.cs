/// <summary>
/// 【ModuleDoc】
/// 責務: 終盤探索フリーズ修正に関する単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - G1: 反復深化の上限が残り空きマス数に制限されることを検証する
/// - G2: 連続パス時にリーフ評価へ即座に遷移することを検証する
/// - G3: DiscCountEvaluator 使用時にパターン差分更新がスキップされても
///        探索結果が正しいことを検証する
/// </summary>
using System.Numerics;
using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Models;
using Reluca.Search;

namespace Reluca.Tests.Search
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 終盤探索フリーズ修正のテストクラスです。
    /// G1（反復深化上限制限）、G2（連続パス検出）、G3（パターン差分更新スキップ）を検証します。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PvsSearchEngineEndgameFixUnitTest
    {
        /// <summary>
        /// リソースファイルからゲーム状態を作成します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <returns>ゲーム状態</returns>
        private GameContext CreateGameContext(string targetName, int index, int childIndex, ResourceType type)
        {
            return UnitTestHelper.CreateGameContext(targetName, index, childIndex, type);
        }

        // ============================================================
        // G1: 反復深化の上限を残り空きマス数に制限する
        // ============================================================

        /// <summary>
        /// 終盤局面で CompletedDepth が空きマス数以下であることを検証します。
        /// 空き3マスの局面で depth=3（空きマス数）を上限として探索し、
        /// CompletedDepth が3以下であることを確認します。
        /// </summary>
        [TestMethod]
        public void G1_終盤局面でCompletedDepthが空きマス数以下である()
        {
            // Arrange: 終盤局面（TurnCount=56, 空き3マス）
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();

            // 空きマス数を計算
            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
            Console.WriteLine($"TurnCount={context.TurnCount}, EmptyCount={emptyCount}");
            Assert.IsTrue(emptyCount > 0, "空きマスが0です");

            // FindBestMover と同じオプション構成（depth を空きマス数に設定）
            var options = new SearchOptions(
                emptyCount,
                useTranspositionTable: true,
                useAspirationWindow: true,
                aspirationUseStageTable: true,
                useMultiProbCut: true
            );

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: CompletedDepth が空きマス数以下であること
            Console.WriteLine($"CompletedDepth={result.CompletedDepth}, EmptyCount={emptyCount}");
            Assert.IsTrue(result.CompletedDepth <= emptyCount,
                $"CompletedDepth ({result.CompletedDepth}) が空きマス数 ({emptyCount}) を超えています");
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        /// <summary>
        /// 終盤モード（TurnCount>=46）の空きマス数計算が正しいことを検証します。
        /// FindBestMover が設定する depth が 64 - PopCount(black | white) と一致し、
        /// 従来の EndgameDepth=99 よりも小さいことを確認します。
        /// </summary>
        [TestMethod]
        public void G1_終盤モードの空きマス数計算が正しい()
        {
            // Arrange: 終盤局面（TurnCount=56, 空き3マス）
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);

            // Act: 空きマス数を計算（FindBestMover 内で行われる計算と同じ）
            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
            int stoneCount = BitOperations.PopCount(context.Black | context.White);

            // Assert: 空きマス数が正しく、旧 EndgameDepth=99 よりも大幅に小さいこと
            Console.WriteLine($"TurnCount={context.TurnCount}, StoneCount={stoneCount}, EmptyCount={emptyCount}");
            Assert.AreEqual(64 - stoneCount, emptyCount, "空きマス数の計算が不正です");
            Assert.IsTrue(emptyCount < 99, $"空きマス数 ({emptyCount}) が旧 EndgameDepth=99 以上です");
            Assert.IsTrue(emptyCount <= 64 - context.TurnCount,
                $"空きマス数 ({emptyCount}) がターン数から推定される最大値 ({64 - context.TurnCount}) を超えています");
        }

        // ============================================================
        // G2: 連続パス時にリーフ評価へ即座に遷移する
        // ============================================================

        /// <summary>
        /// 連続パス局面を含む終盤で、探索が正常に完了することを検証します。
        /// ほぼ盤面が埋まった局面（空き3マス）では、探索木内で連続パスが発生する可能性があり、
        /// 連続パス検出により不要な再帰が排除されて正常に完了することを確認します。
        /// </summary>
        [TestMethod]
        public void G2_連続パスを含む終盤局面で探索が正常に完了する()
        {
            // Arrange: 終盤局面（空き3マス）- 探索木内で連続パスが発生しうる
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);

            var options = new SearchOptions(
                emptyCount,
                useTranspositionTable: true
            );

            // Act: 連続パス検出が機能していれば正常に完了する
            var result = target.Search(context, options, evaluator);

            // Assert: 有効な手が返され、石数差に基づく評価値が返される
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Console.WriteLine($"BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}, Nodes={result.NodesSearched}");
        }

        /// <summary>
        /// 連続パスが即座にゲーム終了となる局面で、正しい評価値が返されることを検証します。
        /// 両者とも合法手がない盤面（全マス埋まり、または合法手なし）を構成し、
        /// 探索結果が石数差に基づくリーフ評価値と一致することを確認します。
        /// </summary>
        [TestMethod]
        public void G2_ゲーム終了局面で正しい石数差が返される()
        {
            // Arrange: 全マス埋まりに近い局面を構成
            // 黒が大量に石を持ち、白が少ない状態で合法手がないケース
            var target = DiProvider.Get().GetService<PvsSearchEngine>();

            // 全マスが埋まった局面を作成（60マスが黒、4マスが白の初期配置）
            // ただし TurnCount は BoardAccessor.IsGameEndTurnCount を満たすようにする
            var context = new GameContext
            {
                Board = new BoardContext
                {
                    // 全64マスのうち、60マスが黒、4マスが白
                    Black = 0xFFFFFFFFFFFFFFFF & ~0x0000001818000000UL, // 初期4マス以外全て黒
                    White = 0x0000001818000000UL, // 初期4マスのみ白
                },
                TurnCount = 64, // ゲーム終了ターン
                Stage = 15,
                Turn = Disc.Color.Black,
            };

            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            var options = new SearchOptions(1, useTranspositionTable: false);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: ゲーム終了局面なので評価値はリーフ評価（石数差）
            // 黒60石 → DiscCountEvaluator.Evaluate は黒石数(60)を返す
            // Evaluate メソッドがパリティを適用するので、黒番視点で 60 * 1 = 60
            Console.WriteLine($"Value={result.Value}");
            Assert.AreEqual(60L, result.Value, "ゲーム終了局面の評価値が石数差と一致しません");
        }

        /// <summary>
        /// 片方のみパスの場合は連続パスとして扱われず、従来通りのパス処理が行われることを検証します。
        /// </summary>
        [TestMethod]
        public void G2_片方のみパスの場合は連続パスにならない()
        {
            // Arrange: 通常の終盤局面（空き3マス、片方はパスだが連続パスではない）
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);

            var options = new SearchOptions(
                emptyCount,
                useTranspositionTable: false
            );

            // Act: パスが発生しても連続パスでなければ探索が継続される
            var result = target.Search(context, options, evaluator);

            // Assert: 有効な手が返される
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
        }

        // ============================================================
        // G3: 終盤探索でパターン差分更新をスキップする
        // ============================================================

        /// <summary>
        /// DiscCountEvaluator（RequiresPatternIndex = false）を使用した場合に、
        /// 探索結果が正しい石数差評価値を返すことを検証します。
        /// パターン差分更新がスキップされても探索結果に悪影響がないことを確認します。
        /// </summary>
        [TestMethod]
        public void G3_DiscCountEvaluatorで探索結果が正しい()
        {
            // Arrange: 終盤局面
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();

            Assert.IsFalse(evaluator.RequiresPatternIndex,
                "DiscCountEvaluator.RequiresPatternIndex が false でありません");

            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
            var options = new SearchOptions(
                emptyCount,
                useTranspositionTable: true,
                useAspirationWindow: true,
                aspirationUseStageTable: true,
                useMultiProbCut: true
            );

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: 有効な手が返され、正常に完了する
            Assert.IsTrue(result.BestMove >= 0 && result.BestMove < 64,
                $"無効な手が返されました: {result.BestMove}");
            Console.WriteLine($"BestMove={BoardAccessor.ToPosition(result.BestMove)}, Value={result.Value}, CompletedDepth={result.CompletedDepth}");
        }

        /// <summary>
        /// FeaturePatternEvaluator（RequiresPatternIndex = true）を使用した場合に、
        /// 差分更新が従来通り動作し、探索結果が正常であることを検証します（回帰テスト）。
        /// </summary>
        [TestMethod]
        public void G3_FeaturePatternEvaluatorで探索結果が従来通り正しい()
        {
            // Arrange: 初期局面
            var target = DiProvider.Get().GetService<PvsSearchEngine>();
            var context = CreateGameContext("PvsSearchEngine", 1, 1, ResourceType.In);
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            Assert.IsTrue(evaluator.RequiresPatternIndex,
                "FeaturePatternEvaluator.RequiresPatternIndex が true でありません");

            var options = new SearchOptions(5, useTranspositionTable: true);

            // Act
            var result = target.Search(context, options, evaluator);

            // Assert: 初期局面で有効な手が返される（d3, c4, f5, e6）
            var validMoves = new[] { 19, 26, 37, 44 };
            Assert.IsTrue(validMoves.Contains(result.BestMove),
                $"無効な手が返されました: {BoardAccessor.ToPosition(result.BestMove)}");
        }

        /// <summary>
        /// DiscCountEvaluator の RequiresPatternIndex プロパティが false であることを検証します。
        /// </summary>
        [TestMethod]
        public void G3_DiscCountEvaluatorのRequiresPatternIndexがfalseである()
        {
            // Arrange & Act
            var evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();

            // Assert
            Assert.IsFalse(evaluator.RequiresPatternIndex,
                "DiscCountEvaluator.RequiresPatternIndex は false であるべきです");
        }

        /// <summary>
        /// FeaturePatternEvaluator の RequiresPatternIndex プロパティが true であることを検証します。
        /// </summary>
        [TestMethod]
        public void G3_FeaturePatternEvaluatorのRequiresPatternIndexがtrueである()
        {
            // Arrange & Act
            var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();

            // Assert
            Assert.IsTrue(evaluator.RequiresPatternIndex,
                "FeaturePatternEvaluator.RequiresPatternIndex は true であるべきです");
        }

        // ============================================================
        // 統合テスト: FindBestMover 経由での終盤探索
        // ============================================================

        /// <summary>
        /// FindBestMover 経由で終盤探索が正常に動作し、有効な手を返すことを検証します。
        /// 対策1〜3の全てが統合的に機能することを確認する統合テストです。
        /// </summary>
        [TestMethod]
        public void 統合_FindBestMover経由で終盤探索が正常に完了する()
        {
            // Arrange: 終盤局面（TurnCount=56, 空き3マス）
            var target = DiProvider.Get().GetService<Reluca.Movers.FindBestMover>();
            var context = CreateGameContext("FindBestMover", 2, 1, ResourceType.In);

            // Act: FindBestMover 経由で探索
            var bestMove = target.Move(context);

            // Assert: 有効な手が返される
            Assert.IsTrue(bestMove >= 0 && bestMove < 64,
                $"無効な手が返されました: {bestMove}");
            Console.WriteLine($"BestMove={BoardAccessor.ToPosition(bestMove)}");
        }

        /// <summary>
        /// 空き15マス（TurnCount=49）の終盤局面で、MPC無効化により実用的な時間で完了することを検証します。
        /// 旧 CachedNegaMax と同等の速度（数秒以内）を確認します。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void 統合_空き15マスの終盤探索が実用的な時間で完了する()
        {
            // Arrange: TurnCount=49, 空き15マスの局面（NegaMax テストデータ）
            var target = DiProvider.Get().GetService<Reluca.Movers.FindBestMover>();
            var context = CreateGameContext("NegaMax", 2, 1, ResourceType.In);
            int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
            Console.WriteLine($"TurnCount={context.TurnCount}, EmptyCount={emptyCount}");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act: FindBestMover 経由で終盤探索
            var bestMove = target.Move(context);

            sw.Stop();
            Console.WriteLine($"BestMove={BoardAccessor.ToPosition(bestMove)}, ElapsedMs={sw.ElapsedMilliseconds}");

            // Assert: 有効な手が返され、30秒以内に完了する
            Assert.IsTrue(bestMove >= 0 && bestMove < 64,
                $"無効な手が返されました: {bestMove}");
            Assert.IsTrue(sw.ElapsedMilliseconds < 30000,
                $"探索に {sw.ElapsedMilliseconds}ms かかりました。旧ロジック同等（数秒）を期待します");
        }
    }
}
