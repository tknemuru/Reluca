/// <summary>
/// 【ModuleDoc】
/// 責務: FeaturePatternExtractor の差分更新（逆引きテーブルと3進数インデックス差分計算）のテストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// 備考:
/// - 差分更新後のパターンインデックスがフルスキャン結果と一致することを検証する
/// - 逆引きテーブルの構築が正しいことを検証する
/// - 複数手シーケンスでの差分更新と復元が正しいことを検証する
/// </summary>
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Models;
using System.Numerics;

namespace Reluca.Tests.Evaluates
{
    /// <summary>
    /// FeaturePatternExtractor の差分更新テストクラスです。
    /// ExtractNoAlloc のシングルスレッド前提の内部バッファを使用するため、並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FeaturePatternIncrementalUpdateTest
    {
        /// <summary>
        /// テスト対象の FeaturePatternExtractor
        /// </summary>
        private readonly FeaturePatternExtractor _extractor;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FeaturePatternIncrementalUpdateTest()
        {
            _extractor = DiProvider.Get().GetService<FeaturePatternExtractor>()!;
        }

        /// <summary>
        /// 逆引きテーブルが全マスに対して構築されている
        /// </summary>
        [TestMethod]
        public void 逆引きテーブルが全マスに対して構築されている()
        {
            // 全 64 マスについて逆引き情報が取得できる
            for (int square = 0; square < 64; square++)
            {
                var mappings = _extractor.GetSquarePatterns(square);
                Assert.IsNotNull(mappings, $"マス {square} の逆引き情報が null です");
            }
        }

        /// <summary>
        /// 中央マスは複数のパターンに属している
        /// </summary>
        [TestMethod]
        public void 中央マスは複数のパターンに属している()
        {
            // d4 (index 27) はオセロの中央マスであり、多くのパターンに属するはず
            var mappings = _extractor.GetSquarePatterns(27);
            Assert.IsTrue(mappings.Length > 0, "d4 は少なくとも1つのパターンに属するべきです");
        }

        /// <summary>
        /// 単一着手の差分更新がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void 単一着手の差分更新がフルスキャンと一致する()
        {
            // Arrange: 初期局面
            var context = CreateInitialPosition();

            // フルスキャンでパターンインデックスを初期化
            _extractor.ExtractNoAlloc(context.Board);

            // 黒番の合法手を取得
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(context.Black, context.White);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
            Assert.IsTrue(moves.Count > 0, "合法手がありません");

            foreach (var move in moves)
            {
                // 差分更新前にフルスキャンでベースラインを取得
                _extractor.ExtractNoAlloc(context.Board);
                var baselineIndices = CopyResults(_extractor.PreallocatedResults);

                // 裏返し石を計算
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(context.Black, context.White, move);

                // 差分更新を適用
                // 着手位置: 空→黒 (delta = +1)
                ApplyDeltaForSquare(move, 1);
                // 裏返し位置: 白→黒 (delta = +2)
                ulong tmpFlipped = flipped;
                while (tmpFlipped != 0)
                {
                    int sq = BitOperations.TrailingZeroCount(tmpFlipped);
                    ApplyDeltaForSquare(sq, 2);
                    tmpFlipped &= tmpFlipped - 1;
                }

                // 盤面を実際に更新してフルスキャンで期待値を計算
                var childContext = CreateInitialPosition();
                ulong moveBit = 1UL << move;
                childContext.Black |= moveBit | flipped;
                childContext.White &= ~flipped;
                childContext.Turn = Disc.Color.White;

                _extractor.IncrementalMode = false;
                var expectedIndices = CopyResults(_extractor.ExtractNoAlloc(childContext.Board));

                // 差分更新結果と比較（IncrementalMode を一時的に true にして結果を取得）
                _extractor.IncrementalMode = true;
                var incrementalIndices = CopyResults(_extractor.PreallocatedResults);
                _extractor.IncrementalMode = false;

                // 検証
                AssertIndicesEqual(expectedIndices, incrementalIndices, $"Move {move}");

                // 復元（ベースラインに戻す）
                RestoreResults(baselineIndices);
            }
        }

        /// <summary>
        /// 複数手シーケンスで差分更新がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void 複数手シーケンスで差分更新がフルスキャンと一致する()
        {
            // Arrange: 初期局面から5手分の差分更新
            var context = CreateInitialPosition();
            _extractor.ExtractNoAlloc(context.Board);

            for (int step = 0; step < 5; step++)
            {
                var (player, opponent) = context.Turn == Disc.Color.Black
                    ? (context.Black, context.White)
                    : (context.White, context.Black);

                ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
                if (movesBitboard == 0) break;

                var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
                int move = moves[0];
                bool isBlackTurn = context.Turn == Disc.Color.Black;

                // 裏返し石を計算
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);

                // 差分更新を適用
                int moveDelta = isBlackTurn ? 1 : -1;
                int flipDelta = isBlackTurn ? 2 : -2;

                ApplyDeltaForSquare(move, moveDelta);
                ulong tmpFlipped = flipped;
                while (tmpFlipped != 0)
                {
                    int sq = BitOperations.TrailingZeroCount(tmpFlipped);
                    ApplyDeltaForSquare(sq, flipDelta);
                    tmpFlipped &= tmpFlipped - 1;
                }

                // 盤面を実際に更新
                ApplyMove(context, move, flipped);

                // フルスキャンと比較
                _extractor.IncrementalMode = false;
                var expectedIndices = CopyResults(_extractor.ExtractNoAlloc(context.Board));
                var incrementalIndices = CopyResults(_extractor.PreallocatedResults);

                AssertIndicesEqual(expectedIndices, incrementalIndices, $"Step {step + 1}");
            }

            // IncrementalMode をリセット
            _extractor.IncrementalMode = false;
        }

        /// <summary>
        /// 差分更新と復元の往復でインデックスが元に戻る
        /// </summary>
        [TestMethod]
        public void 差分更新と復元の往復でインデックスが元に戻る()
        {
            // Arrange: 初期局面
            var context = CreateInitialPosition();
            _extractor.ExtractNoAlloc(context.Board);
            var originalIndices = CopyResults(_extractor.PreallocatedResults);

            // 黒番の最初の合法手
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(context.Black, context.White);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
            int move = moves[0];

            ulong flipped = BitboardMobilityGenerator.ComputeFlipped(context.Black, context.White, move);

            // 差分更新を適用
            ApplyDeltaForSquare(move, 1); // 空→黒
            ulong tmpFlipped = flipped;
            while (tmpFlipped != 0)
            {
                int sq = BitOperations.TrailingZeroCount(tmpFlipped);
                ApplyDeltaForSquare(sq, 2); // 白→黒
                tmpFlipped &= tmpFlipped - 1;
            }

            // 変更されていることを確認
            var changedIndices = CopyResults(_extractor.PreallocatedResults);
            bool anyChanged = false;
            foreach (var patternType in originalIndices.Keys)
            {
                for (int i = 0; i < originalIndices[patternType].Length; i++)
                {
                    if (originalIndices[patternType][i] != changedIndices[patternType][i])
                    {
                        anyChanged = true;
                        break;
                    }
                }
                if (anyChanged) break;
            }
            Assert.IsTrue(anyChanged, "差分更新後にインデックスが変化していません");

            // 逆の差分更新で復元
            // 裏返し位置: 黒→白 (delta = -2)
            tmpFlipped = flipped;
            while (tmpFlipped != 0)
            {
                int sq = BitOperations.TrailingZeroCount(tmpFlipped);
                ApplyDeltaForSquare(sq, -2);
                tmpFlipped &= tmpFlipped - 1;
            }
            // 着手位置: 黒→空 (delta = -1)
            ApplyDeltaForSquare(move, -1);

            // 検証: 元に戻っている
            var restoredIndices = CopyResults(_extractor.PreallocatedResults);
            AssertIndicesEqual(originalIndices, restoredIndices, "復元後");
        }

        /// <summary>
        /// 指定マスの差分更新をパターンインデックスに適用します。
        /// </summary>
        /// <param name="square">マス（0-63）</param>
        /// <param name="deltaMultiplier">差分の方向と大きさ</param>
        private void ApplyDeltaForSquare(int square, int deltaMultiplier)
        {
            var mappings = _extractor.GetSquarePatterns(square);
            var results = _extractor.PreallocatedResults;

            for (int i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                var arr = results[mapping.PatternType];
                arr[mapping.SubPatternIndex] += deltaMultiplier * mapping.TernaryWeight;
            }
        }

        /// <summary>
        /// パターンインデックスの辞書をディープコピーします。
        /// </summary>
        /// <param name="source">コピー元</param>
        /// <returns>コピーされた辞書</returns>
        private static Dictionary<FeaturePattern.Type, int[]> CopyResults(Dictionary<FeaturePattern.Type, int[]> source)
        {
            var copy = new Dictionary<FeaturePattern.Type, int[]>();
            foreach (var kv in source)
            {
                copy[kv.Key] = (int[])kv.Value.Clone();
            }
            return copy;
        }

        /// <summary>
        /// パターンインデックスの辞書を復元します。
        /// </summary>
        /// <param name="source">復元元の値</param>
        private void RestoreResults(Dictionary<FeaturePattern.Type, int[]> source)
        {
            var results = _extractor.PreallocatedResults;
            foreach (var kv in source)
            {
                Array.Copy(kv.Value, results[kv.Key], kv.Value.Length);
            }
        }

        /// <summary>
        /// 2つのパターンインデックス辞書が一致することを検証します。
        /// </summary>
        /// <param name="expected">期待値</param>
        /// <param name="actual">実際の値</param>
        /// <param name="context">エラーメッセージのコンテキスト</param>
        private static void AssertIndicesEqual(
            Dictionary<FeaturePattern.Type, int[]> expected,
            Dictionary<FeaturePattern.Type, int[]> actual,
            string context)
        {
            foreach (var patternType in expected.Keys)
            {
                Assert.IsTrue(actual.ContainsKey(patternType),
                    $"{context}: パターン {patternType} が結果に存在しません");

                var expectedArr = expected[patternType];
                var actualArr = actual[patternType];
                Assert.AreEqual(expectedArr.Length, actualArr.Length,
                    $"{context}: パターン {patternType} の配列長が異なります");

                for (int i = 0; i < expectedArr.Length; i++)
                {
                    Assert.AreEqual(expectedArr[i], actualArr[i],
                        $"{context}: パターン {patternType}[{i}] が異なります。Expected={expectedArr[i]}, Actual={actualArr[i]}");
                }
            }
        }

        /// <summary>
        /// 初期局面（オセロの標準開始位置）を作成します。
        /// </summary>
        /// <returns>初期局面のゲーム状態</returns>
        private static GameContext CreateInitialPosition()
        {
            return new GameContext
            {
                Board = new BoardContext
                {
                    Black = (1UL << 28) | (1UL << 35), // e4, d5
                    White = (1UL << 27) | (1UL << 36)  // d4, e5
                },
                Turn = Disc.Color.Black
            };
        }

        /// <summary>
        /// 着手を盤面に反映します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="move">着手位置（0-63）</param>
        /// <param name="flipped">裏返された石のビットボード</param>
        private static void ApplyMove(GameContext context, int move, ulong flipped)
        {
            ulong moveBit = 1UL << move;
            if (context.Turn == Disc.Color.Black)
            {
                context.Black |= moveBit | flipped;
                context.White &= ~flipped;
                context.Turn = Disc.Color.White;
            }
            else
            {
                context.White |= moveBit | flipped;
                context.Black &= ~flipped;
                context.Turn = Disc.Color.Black;
            }
        }
    }
}
