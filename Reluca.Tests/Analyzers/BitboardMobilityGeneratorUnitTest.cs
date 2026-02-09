using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System.Numerics;

namespace Reluca.Tests.Analyzers
{
    /// <summary>
    /// BitboardMobilityGenerator の単体テストクラスです。
    /// 既存の MoveAndReverseUpdater ベースの合法手生成結果との一致を検証します。
    /// </summary>
    [TestClass]
    public class BitboardMobilityGeneratorUnitTest
    {
        /// <summary>
        /// 既存の MoveAndReverseUpdater（比較用）
        /// </summary>
        private readonly MoveAndReverseUpdater _updater;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BitboardMobilityGeneratorUnitTest()
        {
            _updater = DiProvider.Get().GetService<MoveAndReverseUpdater>()!;
        }

        /// <summary>
        /// 初期局面でビットボード合法手生成が既存実装と一致する
        /// </summary>
        [TestMethod]
        public void 初期局面で合法手が一致する()
        {
            // Arrange: 初期局面（黒番）
            var context = CreateInitialPosition();
            var (player, opponent) = (context.Black, context.White);

            // Act
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            var bitboardMoves = BitboardMobilityGenerator.ToMoveList(movesBitboard);

            // 既存実装で合法手を取得
            var legacyMoves = GetLegacyMoves(context);

            // Assert
            bitboardMoves.Sort();
            legacyMoves.Sort();
            CollectionAssert.AreEqual(legacyMoves, bitboardMoves,
                $"初期局面の合法手が一致しません。Legacy: [{string.Join(",", legacyMoves)}] Bitboard: [{string.Join(",", bitboardMoves)}]");
        }

        /// <summary>
        /// 初期局面で合法手数が一致する
        /// </summary>
        [TestMethod]
        public void 初期局面で合法手数が一致する()
        {
            // Arrange
            var context = CreateInitialPosition();
            var (player, opponent) = (context.Black, context.White);

            // Act
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            int bitboardCount = BitboardMobilityGenerator.CountMoves(movesBitboard);

            var legacyMoves = GetLegacyMoves(context);

            // Assert
            Assert.AreEqual(legacyMoves.Count, bitboardCount);
        }

        /// <summary>
        /// 中盤局面でビットボード合法手生成が既存実装と一致する
        /// </summary>
        [TestMethod]
        public void 中盤局面で合法手が一致する()
        {
            // Arrange: 中盤局面（多数の石が配置された状態）
            var context = CreateMidgamePosition();

            // 黒番
            VerifyMovesMatch(context, Disc.Color.Black);

            // 白番
            VerifyMovesMatch(context, Disc.Color.White);
        }

        /// <summary>
        /// 終盤局面でビットボード合法手生成が既存実装と一致する
        /// </summary>
        [TestMethod]
        public void 終盤局面で合法手が一致する()
        {
            // Arrange: 終盤局面（ほぼ埋まった状態）
            var context = CreateEndgamePosition();

            // 黒番
            VerifyMovesMatch(context, Disc.Color.Black);

            // 白番
            VerifyMovesMatch(context, Disc.Color.White);
        }

        /// <summary>
        /// パス局面（合法手なし）でビットボードが正しく 0 を返す
        /// </summary>
        [TestMethod]
        public void パス局面で合法手が0個になる()
        {
            // Arrange: 全マスが埋まった局面（合法手なし）
            var context = new GameContext
            {
                Board = new BoardContext
                {
                    Black = 0xFFFFFFFF00000000UL,
                    White = 0x00000000FFFFFFFFUL
                },
                Turn = Disc.Color.Black
            };

            // Act
            ulong moves = BitboardMobilityGenerator.GenerateMoves(context.Black, context.White);

            // Assert
            Assert.AreEqual(0UL, moves, "全マスが埋まった局面で合法手があります");
        }

        /// <summary>
        /// 初期局面で ComputeFlipped が既存の裏返し処理と一致する
        /// </summary>
        [TestMethod]
        public void 初期局面で裏返し石が一致する()
        {
            // Arrange
            var context = CreateInitialPosition();
            var (player, opponent) = (context.Black, context.White);

            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);

            foreach (var move in moves)
            {
                // Act: ビットボード版で裏返し石を計算
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);

                // 既存実装で裏返し石を計算
                ulong legacyFlipped = GetLegacyFlipped(context, move);

                // Assert
                Assert.AreEqual(legacyFlipped, flipped,
                    $"Move {move}: 裏返し石が一致しません。Legacy: 0x{legacyFlipped:X16} Bitboard: 0x{flipped:X16}");
            }
        }

        /// <summary>
        /// 中盤局面で ComputeFlipped が全合法手について既存実装と一致する
        /// </summary>
        [TestMethod]
        public void 中盤局面で裏返し石が一致する()
        {
            var context = CreateMidgamePosition();

            // 黒番
            VerifyFlippedMatch(context, Disc.Color.Black);

            // 白番
            VerifyFlippedMatch(context, Disc.Color.White);
        }

        /// <summary>
        /// 複数の局面でビットボード合法手生成と既存実装の網羅的な一致を検証する
        /// </summary>
        [TestMethod]
        public void 複数局面の網羅検証()
        {
            // 様々な局面パターンを検証
            var positions = new[]
            {
                CreateInitialPosition(),
                CreateMidgamePosition(),
                CreateEndgamePosition(),
                CreateCornerPosition(),
                CreateEdgePosition(),
            };

            foreach (var pos in positions)
            {
                VerifyMovesMatch(pos, Disc.Color.Black);
                VerifyMovesMatch(pos, Disc.Color.White);
            }
        }

        /// <summary>
        /// コーナー周辺の局面でビットボード合法手生成が正しく動作する
        /// </summary>
        [TestMethod]
        public void コーナー局面で合法手が一致する()
        {
            var context = CreateCornerPosition();
            VerifyMovesMatch(context, Disc.Color.Black);
            VerifyMovesMatch(context, Disc.Color.White);
        }

        /// <summary>
        /// 辺沿いの局面でビットボード合法手生成が正しく動作する
        /// </summary>
        [TestMethod]
        public void 辺沿い局面で合法手が一致する()
        {
            var context = CreateEdgePosition();
            VerifyMovesMatch(context, Disc.Color.Black);
            VerifyMovesMatch(context, Disc.Color.White);
        }

        /// <summary>
        /// 空の盤面で合法手が 0 になる
        /// </summary>
        [TestMethod]
        public void 空の盤面で合法手が0になる()
        {
            ulong moves = BitboardMobilityGenerator.GenerateMoves(0UL, 0UL);
            Assert.AreEqual(0UL, moves);
        }

        /// <summary>
        /// ComputeFlipped で裏返し石がない位置に着手すると 0 を返す
        /// </summary>
        [TestMethod]
        public void 裏返し石がない位置で0を返す()
        {
            // Arrange: 空マスだが合法手ではない位置
            var context = CreateInitialPosition();
            // 例: a1 (index 0) は初期局面では合法手ではない
            ulong flipped = BitboardMobilityGenerator.ComputeFlipped(context.Black, context.White, 0);
            Assert.AreEqual(0UL, flipped, "a1 に裏返し石があるのは不正です");
        }

        /// <summary>
        /// 指定された手番と色で合法手が一致することを検証するヘルパーメソッドです。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">検証する手番</param>
        private void VerifyMovesMatch(GameContext context, Disc.Color turn)
        {
            var (player, opponent) = turn == Disc.Color.Black
                ? (context.Black, context.White)
                : (context.White, context.Black);

            var orgTurn = context.Turn;
            context.Turn = turn;

            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            var bitboardMoves = BitboardMobilityGenerator.ToMoveList(movesBitboard);

            var legacyMoves = GetLegacyMoves(context);

            context.Turn = orgTurn;

            bitboardMoves.Sort();
            legacyMoves.Sort();
            CollectionAssert.AreEqual(legacyMoves, bitboardMoves,
                $"Turn={turn}: 合法手が一致しません。Legacy: [{string.Join(",", legacyMoves)}] Bitboard: [{string.Join(",", bitboardMoves)}]");
        }

        /// <summary>
        /// 指定された手番と色で裏返し石が一致することを検証するヘルパーメソッドです。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">検証する手番</param>
        private void VerifyFlippedMatch(GameContext context, Disc.Color turn)
        {
            var (player, opponent) = turn == Disc.Color.Black
                ? (context.Black, context.White)
                : (context.White, context.Black);

            var orgTurn = context.Turn;
            context.Turn = turn;

            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);

            foreach (var move in moves)
            {
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);
                ulong legacyFlipped = GetLegacyFlipped(context, move);

                Assert.AreEqual(legacyFlipped, flipped,
                    $"Turn={turn} Move={move}: 裏返し石が一致しません。Legacy: 0x{legacyFlipped:X16} Bitboard: 0x{flipped:X16}");
            }

            context.Turn = orgTurn;
        }

        /// <summary>
        /// 既存の MoveAndReverseUpdater を使用して合法手リストを取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>合法手リスト</returns>
        private List<int> GetLegacyMoves(GameContext context)
        {
            var moves = new List<int>();
            for (int i = 0; i < Board.AllLength; i++)
            {
                if (_updater.Update(context, i))
                {
                    moves.Add(i);
                }
            }
            return moves;
        }

        /// <summary>
        /// 既存の MoveAndReverseUpdater を使用して、指定位置に着手した場合の裏返し石を取得します。
        /// MoveAndReverseUpdater.Update を実行モード（move=-1）で呼び出し、変化した石を特定します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="move">着手位置</param>
        /// <returns>裏返された石のビットボード</returns>
        private ulong GetLegacyFlipped(GameContext context, int move)
        {
            // 着手前の状態を保存
            ulong prevBlack = context.Black;
            ulong prevWhite = context.White;

            // 実行モードで着手
            context.Move = move;
            _updater.Update(context);

            // 裏返し石 = 変化した石（着手位置を除く）
            ulong moveBit = 1UL << move;

            // 手番側: 着手位置以外で新たに追加された石 = 裏返し石
            ulong turnDiscsAfter = context.Turn == Disc.Color.Black
                ? context.Black : context.White;
            ulong turnDiscsBefore = context.Turn == Disc.Color.Black
                ? prevBlack : prevWhite;
            ulong flipped = (turnDiscsAfter & ~turnDiscsBefore) & ~moveBit;

            // 盤面を復元
            context.Black = prevBlack;
            context.White = prevWhite;

            return flipped;
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
        /// 中盤局面（20手目前後）を作成します。
        /// d3に黒が着手した初期局面から数手進めた状態を模擬します。
        /// </summary>
        /// <returns>中盤局面のゲーム状態</returns>
        private static GameContext CreateMidgamePosition()
        {
            // 実際のオセロ対局から取得した中盤局面
            // 黒: d3,d4,d5,e3,e4,e5,f4,f5,c5,c4 周辺
            return new GameContext
            {
                Board = new BoardContext
                {
                    Black = (1UL << 19) | (1UL << 27) | (1UL << 28) | (1UL << 35) | (1UL << 20) | (1UL << 29) | (1UL << 34),
                    White = (1UL << 36) | (1UL << 37) | (1UL << 26) | (1UL << 21) | (1UL << 18) | (1UL << 44)
                },
                Turn = Disc.Color.Black
            };
        }

        /// <summary>
        /// 終盤局面（空マスが少ない状態）を作成します。
        /// 黒と白の石が隣接して配置された状態を作成します。
        /// </summary>
        /// <returns>終盤局面のゲーム状態</returns>
        private static GameContext CreateEndgamePosition()
        {
            // 終盤局面: 中央付近が埋まり、縁に空きがある状態
            // 黒と白が交互に隣接するように配置
            return new GameContext
            {
                Board = new BoardContext
                {
                    Black = 0x00003C3C3C3C0000UL, // 中央の4列
                    White = 0x007E424242427E00UL  // 周囲の枠
                },
                Turn = Disc.Color.Black
            };
        }

        /// <summary>
        /// コーナー周辺に石が配置された局面を作成します。
        /// コーナーに黒が配置され、隣接して白が配置された局面です。
        /// </summary>
        /// <returns>コーナー局面のゲーム状態</returns>
        private static GameContext CreateCornerPosition()
        {
            // 左上コーナー: a1(0)に黒, b1(1),a2(8)に白, c1(2),a3(16)に黒
            // 右下コーナー: h8(63)に黒, g8(62),h7(55)に白
            return new GameContext
            {
                Board = new BoardContext
                {
                    Black = (1UL << 0) | (1UL << 2) | (1UL << 16) | (1UL << 63),
                    White = (1UL << 1) | (1UL << 8) | (1UL << 62) | (1UL << 55)
                },
                Turn = Disc.Color.Black
            };
        }

        /// <summary>
        /// 辺沿いに石が配置された局面を作成します。
        /// 1行目に黒と白が交互に配置された局面です。
        /// </summary>
        /// <returns>辺沿い局面のゲーム状態</returns>
        private static GameContext CreateEdgePosition()
        {
            // 1行目: a1(0)黒, b1(1)白, c1(2)黒, d1(3)白, e1(4)黒
            // + 中央に初期配置
            return new GameContext
            {
                Board = new BoardContext
                {
                    Black = (1UL << 0) | (1UL << 2) | (1UL << 4) | (1UL << 28) | (1UL << 35),
                    White = (1UL << 1) | (1UL << 3) | (1UL << 27) | (1UL << 36)
                },
                Turn = Disc.Color.Black
            };
        }
    }
}
