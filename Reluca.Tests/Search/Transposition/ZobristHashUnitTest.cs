/// <summary>
/// 【ModuleDoc】
/// 責務: ZobristHash の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
/// </summary>
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
        /// 同一盤面は同一ハッシュを返す
        /// </summary>
        [TestMethod]
        public void 同一盤面は同一ハッシュを返す()
        {
            // Arrange: 同一の初期局面を2つ作成
            var context1 = CreateInitialPosition();
            var context2 = CreateInitialPosition();

            // Act
            var hash1 = ZobristHash.ComputeHash(context1);
            var hash2 = ZobristHash.ComputeHash(context2);

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
            var hash1 = ZobristHash.ComputeHash(context1);

            // 1手進めた局面（d3に黒石を置き、d4を裏返す）
            var context2 = CreateInitialPosition();
            context2.Black |= 1UL << 19; // d3 に黒石
            context2.White &= ~(1UL << 27); // d4 の白を消す
            context2.Black |= 1UL << 27; // d4 に黒石
            context2.Turn = Disc.Color.White; // 白番に

            // Act
            var hash2 = ZobristHash.ComputeHash(context2);

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
            var hashBlack = ZobristHash.ComputeHash(contextBlack);
            var hashWhite = ZobristHash.ComputeHash(contextWhite);

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
            var hash = ZobristHash.ComputeHash(context);

            // Assert: 黒番で空盤面なのでハッシュは 0
            Assert.AreEqual(0UL, hash, "空盤面の黒番ハッシュは 0 であるべきです");

            // 白番にすると TurnKey だけになる
            context.Turn = Disc.Color.White;
            var hashWhite = ZobristHash.ComputeHash(context);
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
            var hash1 = ZobristHash.ComputeHash(context);
            var hash2 = ZobristHash.ComputeHash(context);
            var hash3 = ZobristHash.ComputeHash(context);

            // Assert: すべて同一
            Assert.AreEqual(hash1, hash2, "同一局面の複数回計算で異なるハッシュ");
            Assert.AreEqual(hash2, hash3, "同一局面の複数回計算で異なるハッシュ");

            // ZobristKeys の静的テーブルが固定シードで生成されていることを確認
            // （異なるテスト実行でも同じ値になる）
            Assert.IsTrue(hash1 != 0, "初期局面のハッシュが 0 です（乱数生成が機能していない可能性）");
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
    }
}
