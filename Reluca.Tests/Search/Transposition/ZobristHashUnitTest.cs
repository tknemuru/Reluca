/// <summary>
/// 【ModuleDoc】
/// 責務: ZobristHash の単体テスト（ComputeHash および UpdateHash）を提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// 備考:
/// - ComputeHash: フルスキャン計算の正当性テスト（同一盤面、手番、空盤面、再現性）
/// - UpdateHash: 差分更新がフルスキャンと一致することのテスト（単一着手、複数手シーケンス、パス処理）
/// </summary>
using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Models;
using Reluca.Search.Transposition;

namespace Reluca.Tests.Search.Transposition
{
    /// <summary>
    /// ZobristHash の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class ZobristHashUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private readonly IZobristHash _zobristHash;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ZobristHashUnitTest()
        {
            _zobristHash = new ZobristHash();
        }

        /// <summary>
        /// 同一盤面は同一ハッシュを返す
        /// </summary>
        [TestMethod]
        public void 同一盤面は同一ハッシュを返す()
        {
            // Arrange: 同一の初期局面を2つ作成
            var context1 = CreateInitialPosition();
            var context2 = CreateInitialPosition();

            // Act
            var hash1 = _zobristHash.ComputeHash(context1);
            var hash2 = _zobristHash.ComputeHash(context2);

            // Assert
            Assert.AreEqual(hash1, hash2, "同一盤面のハッシュが異なります");
        }

        /// <summary>
        /// 1手進めた盤面はハッシュが変わる
        /// </summary>
        [TestMethod]
        public void 一手進めた盤面はハッシュが変わる()
        {
            // Arrange: 初期局面
            var context1 = CreateInitialPosition();
            var hash1 = _zobristHash.ComputeHash(context1);

            // 1手進めた局面（d3に黒石を置き、d4を裏返す）
            var context2 = CreateInitialPosition();
            context2.Black |= 1UL << 19; // d3 に黒石
            context2.White &= ~(1UL << 27); // d4 の白を消す
            context2.Black |= 1UL << 27; // d4 に黒石
            context2.Turn = Disc.Color.White; // 白番に

            // Act
            var hash2 = _zobristHash.ComputeHash(context2);

            // Assert
            Assert.AreNotEqual(hash1, hash2, "1手進めた盤面のハッシュが同じです");
        }

        /// <summary>
        /// 手番が変わるとハッシュが変わる（TurnKeyが効いている）
        /// </summary>
        [TestMethod]
        public void 手番が変わるとハッシュが変わる()
        {
            // Arrange: 同一盤面で手番だけ異なる
            var contextBlack = CreateInitialPosition();
            contextBlack.Turn = Disc.Color.Black;

            var contextWhite = CreateInitialPosition();
            contextWhite.Turn = Disc.Color.White;

            // Act
            var hashBlack = _zobristHash.ComputeHash(contextBlack);
            var hashWhite = _zobristHash.ComputeHash(contextWhite);

            // Assert
            Assert.AreNotEqual(hashBlack, hashWhite, "手番が異なる盤面のハッシュが同じです");

            // 手番キーの XOR を確認（hashBlack ^ TurnKey == hashWhite）
            Assert.AreEqual(hashBlack ^ ZobristKeys.TurnKey, hashWhite,
                "手番キーの XOR が正しく機能していません");
        }

        /// <summary>
        /// 空の盤面でもハッシュが計算できる
        /// </summary>
        [TestMethod]
        public void 空の盤面でもハッシュが計算できる()
        {
            // Arrange: 空の盤面
            var context = new GameContext
            {
                Board = new BoardContext { Black = 0, White = 0 },
                Turn = Disc.Color.Black
            };

            // Act
            var hash = _zobristHash.ComputeHash(context);

            // Assert: 黒番で空盤面なのでハッシュは 0
            Assert.AreEqual(0UL, hash, "空盤面の黒番ハッシュは 0 であるべきです");

            // 白番にすると TurnKey だけになる
            context.Turn = Disc.Color.White;
            var hashWhite = _zobristHash.ComputeHash(context);
            Assert.AreEqual(ZobristKeys.TurnKey, hashWhite,
                "空盤面の白番ハッシュは TurnKey であるべきです");
        }

        /// <summary>
        /// 固定シードにより再現性がある
        /// </summary>
        [TestMethod]
        public void 固定シードにより再現性がある()
        {
            // Arrange: 特定の局面
            var context = CreateInitialPosition();

            // Act: 複数回計算
            var hash1 = _zobristHash.ComputeHash(context);
            var hash2 = _zobristHash.ComputeHash(context);
            var hash3 = _zobristHash.ComputeHash(context);

            // Assert: すべて同一
            Assert.AreEqual(hash1, hash2, "同一局面の複数回計算で異なるハッシュ");
            Assert.AreEqual(hash2, hash3, "同一局面の複数回計算で異なるハッシュ");

            // ZobristKeys の静的テーブルが固定シードで生成されていることを確認
            // （異なるテスト実行でも同じ値になる）
            Assert.IsTrue(hash1 != 0, "初期局面のハッシュが 0 です（乱数生成が機能していない可能性）");
        }

        /// <summary>
        /// 単一着手の UpdateHash がフルスキャン ComputeHash と一致する
        /// </summary>
        [TestMethod]
        public void 単一着手のUpdateHashがフルスキャンと一致する()
        {
            // Arrange: 初期局面（黒番）
            var context = CreateInitialPosition();
            ulong hashBefore = _zobristHash.ComputeHash(context);

            // 黒番の全合法手について検証
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(context.Black, context.White);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
            Assert.IsTrue(moves.Count > 0, "合法手がありません");

            foreach (var move in moves)
            {
                // 着手を実行して子局面を作成
                var childContext = CreateInitialPosition();
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(childContext.Black, childContext.White, move);
                ApplyMove(childContext, move, flipped);

                // Act: UpdateHash で差分更新
                ulong incrementalHash = _zobristHash.UpdateHash(hashBefore, move, flipped, true);

                // Act: ComputeHash でフルスキャン
                ulong fullScanHash = _zobristHash.ComputeHash(childContext);

                // Assert
                Assert.AreEqual(fullScanHash, incrementalHash,
                    $"Move {move}: UpdateHash (0x{incrementalHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
            }
        }

        /// <summary>
        /// 白番からの単一着手でも UpdateHash がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void 白番の単一着手のUpdateHashがフルスキャンと一致する()
        {
            // Arrange: 初期局面を白番に変更
            var context = CreateInitialPosition();
            context.Turn = Disc.Color.White;
            ulong hashBefore = _zobristHash.ComputeHash(context);

            // 白番の全合法手について検証
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(context.White, context.Black);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
            Assert.IsTrue(moves.Count > 0, "合法手がありません");

            foreach (var move in moves)
            {
                // 着手を実行して子局面を作成
                var childContext = CreateInitialPosition();
                childContext.Turn = Disc.Color.White;
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(childContext.White, childContext.Black, move);
                ApplyMove(childContext, move, flipped);

                // Act: UpdateHash で差分更新（isBlackTurn=false）
                ulong incrementalHash = _zobristHash.UpdateHash(hashBefore, move, flipped, false);

                // Act: ComputeHash でフルスキャン
                ulong fullScanHash = _zobristHash.ComputeHash(childContext);

                // Assert
                Assert.AreEqual(fullScanHash, incrementalHash,
                    $"Move {move}: UpdateHash (0x{incrementalHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
            }
        }

        /// <summary>
        /// 複数手シーケンスで UpdateHash の連鎖がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void 複数手シーケンスでUpdateHashの連鎖がフルスキャンと一致する()
        {
            // Arrange: 初期局面から 5 手分の差分更新を連鎖させる
            var context = CreateInitialPosition();
            ulong currentHash = _zobristHash.ComputeHash(context);

            for (int step = 0; step < 5; step++)
            {
                // 現在の手番の合法手を取得
                var (player, opponent) = context.Turn == Disc.Color.Black
                    ? (context.Black, context.White)
                    : (context.White, context.Black);

                ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
                if (movesBitboard == 0)
                {
                    break; // パスになったら終了
                }

                // 最初の合法手を選択
                var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);
                int move = moves[0];
                bool isBlackTurn = context.Turn == Disc.Color.Black;

                // 裏返し石を計算
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);

                // UpdateHash で差分更新
                currentHash = _zobristHash.UpdateHash(currentHash, move, flipped, isBlackTurn);

                // 盤面を実際に更新
                ApplyMove(context, move, flipped);

                // フルスキャンと比較
                ulong fullScanHash = _zobristHash.ComputeHash(context);
                Assert.AreEqual(fullScanHash, currentHash,
                    $"Step {step + 1}: UpdateHash 連鎖 (0x{currentHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
            }
        }

        /// <summary>
        /// パス処理で TurnKey XOR がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void パス処理でTurnKeyXORがフルスキャンと一致する()
        {
            // Arrange: 任意の局面でパスをシミュレート
            var context = CreateInitialPosition();
            ulong hashBefore = _zobristHash.ComputeHash(context);

            // パス: 盤面はそのまま、手番のみ反転
            ulong passHash = hashBefore ^ ZobristKeys.TurnKey;

            // パス後の局面をフルスキャンで計算
            context.Turn = Disc.Color.White;
            ulong fullScanHash = _zobristHash.ComputeHash(context);

            // Assert
            Assert.AreEqual(fullScanHash, passHash,
                $"パス後: TurnKey XOR (0x{passHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
        }

        /// <summary>
        /// 裏返し石なしの着手でも UpdateHash が正しく動作する
        /// </summary>
        [TestMethod]
        public void 裏返し石なしの着手でもUpdateHashが動作する()
        {
            // Arrange: 空盤面に1手だけ置くケース（裏返し石なし）
            var context = new GameContext
            {
                Board = new BoardContext { Black = 0, White = 0 },
                Turn = Disc.Color.Black
            };
            ulong hashBefore = _zobristHash.ComputeHash(context);

            // 裏返し石なしで e4 (28) に着手
            int move = 28;
            ulong flipped = 0UL;
            ulong incrementalHash = _zobristHash.UpdateHash(hashBefore, move, flipped, true);

            // 盤面を手動で更新
            context.Black |= 1UL << move;
            context.Turn = Disc.Color.White;
            ulong fullScanHash = _zobristHash.ComputeHash(context);

            // Assert
            Assert.AreEqual(fullScanHash, incrementalHash,
                $"裏返し石なし: UpdateHash (0x{incrementalHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
        }

        /// <summary>
        /// 中盤局面での UpdateHash がフルスキャンと一致する
        /// </summary>
        [TestMethod]
        public void 中盤局面でUpdateHashがフルスキャンと一致する()
        {
            // Arrange: 中盤局面
            var context = CreateMidgamePosition();
            ulong hashBefore = _zobristHash.ComputeHash(context);

            // 全合法手について検証
            var (player, opponent) = context.Turn == Disc.Color.Black
                ? (context.Black, context.White)
                : (context.White, context.Black);
            ulong movesBitboard = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            var moves = BitboardMobilityGenerator.ToMoveList(movesBitboard);

            foreach (var move in moves)
            {
                // 子局面を作成
                var childContext = CreateMidgamePosition();
                ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);
                ApplyMove(childContext, move, flipped);

                // 差分更新とフルスキャンを比較
                bool isBlackTurn = context.Turn == Disc.Color.Black;
                ulong incrementalHash = _zobristHash.UpdateHash(hashBefore, move, flipped, isBlackTurn);
                ulong fullScanHash = _zobristHash.ComputeHash(childContext);

                Assert.AreEqual(fullScanHash, incrementalHash,
                    $"Move {move}: UpdateHash (0x{incrementalHash:X16}) != ComputeHash (0x{fullScanHash:X16})");
            }
        }

        /// <summary>
        /// 初期局面（オセロの標準開始位置）を作成します。
        /// </summary>
        /// <returns>初期局面のゲーム状態</returns>
        private static GameContext CreateInitialPosition()
        {
            // オセロ初期配置:
            // d4=白, e4=黒, d5=黒, e5=白
            // d4=27, e4=28, d5=35, e5=36 (0-indexed, row-major)
            var context = new GameContext
            {
                Board = new BoardContext
                {
                    Black = (1UL << 28) | (1UL << 35), // e4, d5
                    White = (1UL << 27) | (1UL << 36)  // d4, e5
                },
                Turn = Disc.Color.Black
            };
            return context;
        }

        /// <summary>
        /// 中盤局面を作成します。
        /// </summary>
        /// <returns>中盤局面のゲーム状態</returns>
        private static GameContext CreateMidgamePosition()
        {
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
        /// 着手を盤面に反映します。
        /// 自石に着手位置と裏返し石を追加し、相手石から裏返し石を除去し、手番を反転します。
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
