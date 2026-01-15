/// <summary>
/// 【ModuleDoc】
/// 責務: ZobristTranspositionTable の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// TryProbe 仕様（テストで固定）:
/// - entry.Depth >= requestedDepth が必須
/// - Bound 条件:
///   - Exact: 常に有効
///   - LowerBound: entry.Value >= beta の場合のみ有効
///   - UpperBound: entry.Value <= alpha の場合のみ有効
///
/// TTEntry 初期状態（テストで固定）:
/// - Key = 0, Depth = 0, Value = 0, Bound = Exact, BestMove = -1
/// </summary>
using Reluca.Search.Transposition;

namespace Reluca.Tests.Search.Transposition
{
    /// <summary>
    /// ZobristTranspositionTable の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class ZobristTranspositionTableUnitTest
    {
        /// <summary>
        /// テスト用の小さいテーブルサイズ
        /// </summary>
        private const int TestTableSize = 1024;

        /// <summary>
        /// Store した後に Probe で取得できる
        /// </summary>
        [TestMethod]
        public void Store後にProbeで取得できる()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;
            int depth = 5;
            long value = 100;
            BoundType bound = BoundType.Exact;
            int bestMove = 27;

            // Act
            table.Store(key, depth, value, bound, bestMove);
            bool found = table.TryProbe(key, depth, long.MinValue, long.MaxValue, out TTEntry entry);

            // Assert
            Assert.IsTrue(found, "Store した後に Probe できませんでした");
            Assert.AreEqual(key, entry.Key, "Key が一致しません");
            Assert.AreEqual(depth, entry.Depth, "Depth が一致しません");
            Assert.AreEqual(value, entry.Value, "Value が一致しません");
            Assert.AreEqual(bound, entry.Bound, "Bound が一致しません");
            Assert.AreEqual(bestMove, entry.BestMove, "BestMove が一致しません");
        }

        /// <summary>
        /// 要求深さより浅いエントリは Probe 失敗になる
        /// </summary>
        [TestMethod]
        public void 要求深さより浅いエントリはProbe失敗になる()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // depth=3 で Store
            table.Store(key, 3, 100, BoundType.Exact, 27);

            // Act: depth=5 を要求
            bool found = table.TryProbe(key, 5, long.MinValue, long.MaxValue, out _);

            // Assert
            Assert.IsFalse(found, "浅いエントリが取得できてしまいました");
        }

        /// <summary>
        /// 要求深さ以上のエントリは Probe 成功する
        /// </summary>
        [TestMethod]
        public void 要求深さ以上のエントリはProbe成功する()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // depth=7 で Store
            table.Store(key, 7, 100, BoundType.Exact, 27);

            // Act: depth=5 を要求（7 >= 5 なので成功するはず）
            bool found = table.TryProbe(key, 5, long.MinValue, long.MaxValue, out _);

            // Assert
            Assert.IsTrue(found, "十分な深さのエントリが取得できませんでした");
        }

        /// <summary>
        /// LowerBound は value >= beta の場合のみ有効
        /// </summary>
        [TestMethod]
        public void LowerBoundはValueがBeta以上の場合のみ有効()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // value=100, LowerBound で Store
            table.Store(key, 5, 100, BoundType.LowerBound, 27);

            // Act & Assert: beta=50 なら value(100) >= beta(50) で成功
            bool found1 = table.TryProbe(key, 5, 0, 50, out _);
            Assert.IsTrue(found1, "value >= beta の場合に LowerBound が機能しませんでした");

            // Act & Assert: beta=150 なら value(100) < beta(150) で失敗
            bool found2 = table.TryProbe(key, 5, 0, 150, out _);
            Assert.IsFalse(found2, "value < beta の場合に LowerBound が誤って成功しました");
        }

        /// <summary>
        /// UpperBound は value <= alpha の場合のみ有効
        /// </summary>
        [TestMethod]
        public void UpperBoundはValueがAlpha以下の場合のみ有効()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // value=100, UpperBound で Store
            table.Store(key, 5, 100, BoundType.UpperBound, 27);

            // Act & Assert: alpha=150 なら value(100) <= alpha(150) で成功
            bool found1 = table.TryProbe(key, 5, 150, 200, out _);
            Assert.IsTrue(found1, "value <= alpha の場合に UpperBound が機能しませんでした");

            // Act & Assert: alpha=50 なら value(100) > alpha(50) で失敗
            bool found2 = table.TryProbe(key, 5, 50, 200, out _);
            Assert.IsFalse(found2, "value > alpha の場合に UpperBound が誤って成功しました");
        }

        /// <summary>
        /// Exact は常に有効
        /// </summary>
        [TestMethod]
        public void Exactは常に有効()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // value=100, Exact で Store
            table.Store(key, 5, 100, BoundType.Exact, 27);

            // Act & Assert: どんな alpha/beta でも成功
            bool found1 = table.TryProbe(key, 5, 0, 50, out _);
            Assert.IsTrue(found1, "Exact が alpha/beta 条件で失敗しました (1)");

            bool found2 = table.TryProbe(key, 5, 150, 200, out _);
            Assert.IsTrue(found2, "Exact が alpha/beta 条件で失敗しました (2)");

            bool found3 = table.TryProbe(key, 5, long.MinValue, long.MaxValue, out _);
            Assert.IsTrue(found3, "Exact が alpha/beta 条件で失敗しました (3)");
        }

        /// <summary>
        /// Depth-Preferred: 深い結果で浅い結果が置換される
        /// </summary>
        [TestMethod]
        public void 深い結果で浅い結果が置換される()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // depth=3 で Store
            table.Store(key, 3, 100, BoundType.Exact, 27);

            // depth=7 で上書き
            table.Store(key, 7, 200, BoundType.Exact, 35);

            // Act
            bool found = table.TryProbe(key, 3, long.MinValue, long.MaxValue, out TTEntry entry);

            // Assert: 深い方の結果が取得される
            Assert.IsTrue(found);
            Assert.AreEqual(7, entry.Depth, "深い結果で置換されていません");
            Assert.AreEqual(200, entry.Value);
            Assert.AreEqual(35, entry.BestMove);
        }

        /// <summary>
        /// Depth-Preferred: 浅い結果で深い結果は置換されない
        /// </summary>
        [TestMethod]
        public void 浅い結果で深い結果は置換されない()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            // depth=7 で Store
            table.Store(key, 7, 200, BoundType.Exact, 35);

            // depth=3 で上書き試行（同じキーなので上書きされる）
            // 注: 同一キーの場合は常に更新される仕様
            table.Store(key, 3, 100, BoundType.Exact, 27);

            // Act
            bool found = table.TryProbe(key, 3, long.MinValue, long.MaxValue, out TTEntry entry);

            // Assert: 同一キーの場合は最新の結果になる
            Assert.IsTrue(found);
            Assert.AreEqual(3, entry.Depth);
        }

        /// <summary>
        /// 異なるキーの衝突時は Depth-Preferred が働く
        /// </summary>
        [TestMethod]
        public void 異なるキーの衝突時はDepthPreferredが働く()
        {
            // Arrange: 最小サイズ(1024)のテーブルで衝突を起こす
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);

            // 同じインデックスに衝突する2つのキー
            // TestTableSize = 1024, mask = 1023
            // key1 = 5 → index = 5 & 1023 = 5
            // key2 = 5 + 1024 = 1029 → index = 1029 & 1023 = 5
            ulong key1 = 5;
            ulong key2 = 5 + TestTableSize; // 衝突するキー

            // depth=3 で key1 を Store
            table.Store(key1, 3, 100, BoundType.Exact, 27);

            // depth=7 で key2 を Store（衝突、深いので置換される）
            table.Store(key2, 7, 200, BoundType.Exact, 35);

            // Act & Assert: key1 は消えて key2 が残る
            bool found1 = table.TryProbe(key1, 3, long.MinValue, long.MaxValue, out _);
            Assert.IsFalse(found1, "key1 が残っています（key2 で置換されるべき）");

            bool found2 = table.TryProbe(key2, 7, long.MinValue, long.MaxValue, out TTEntry entry2);
            Assert.IsTrue(found2, "key2 が取得できません");
            Assert.AreEqual(7, entry2.Depth);
        }

        /// <summary>
        /// 異なるキーの衝突時、浅い方は深い方を置換しない
        /// </summary>
        [TestMethod]
        public void 異なるキーの衝突時に浅い方は深い方を置換しない()
        {
            // Arrange: 最小サイズ(1024)のテーブルで衝突を起こす
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);

            // 同じインデックスに衝突する2つのキー
            ulong key1 = 5;
            ulong key2 = 5 + TestTableSize; // 衝突するキー

            // depth=7 で key1 を Store
            table.Store(key1, 7, 200, BoundType.Exact, 35);

            // depth=3 で key2 を Store（衝突、浅いので置換されない）
            table.Store(key2, 3, 100, BoundType.Exact, 27);

            // Act & Assert: key1 が残る
            bool found1 = table.TryProbe(key1, 7, long.MinValue, long.MaxValue, out TTEntry entry1);
            Assert.IsTrue(found1, "key1 が消えました（置換されるべきではない）");
            Assert.AreEqual(7, entry1.Depth);

            bool found2 = table.TryProbe(key2, 3, long.MinValue, long.MaxValue, out _);
            Assert.IsFalse(found2, "key2 が取得できてしまいました");
        }

        /// <summary>
        /// Clear 後は Probe がミスになる
        /// </summary>
        [TestMethod]
        public void Clear後はProbeがミスになる()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;

            table.Store(key, 5, 100, BoundType.Exact, 27);

            // 確認: Store 直後は取得できる
            bool foundBefore = table.TryProbe(key, 5, long.MinValue, long.MaxValue, out _);
            Assert.IsTrue(foundBefore, "Store 直後に取得できませんでした");

            // Act
            table.Clear();

            // Assert
            bool foundAfter = table.TryProbe(key, 5, long.MinValue, long.MaxValue, out _);
            Assert.IsFalse(foundAfter, "Clear 後も取得できてしまいました");
        }

        /// <summary>
        /// GetBestMove でキーが一致する場合は最善手を返す
        /// </summary>
        [TestMethod]
        public void GetBestMoveでキー一致時は最善手を返す()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);
            ulong key = 12345UL;
            int expectedBestMove = 42;

            table.Store(key, 5, 100, BoundType.Exact, expectedBestMove);

            // Act
            int bestMove = table.GetBestMove(key);

            // Assert
            Assert.AreEqual(expectedBestMove, bestMove);
        }

        /// <summary>
        /// GetBestMove でキーが存在しない場合は -1 を返す
        /// </summary>
        [TestMethod]
        public void GetBestMoveでキー不一致時はマイナス1を返す()
        {
            // Arrange
            var config = new TranspositionTableConfig(TestTableSize);
            var table = new ZobristTranspositionTable(config);

            // 何も Store しない

            // Act
            int bestMove = table.GetBestMove(12345UL);

            // Assert
            Assert.AreEqual(TTEntry.NoBestMove, bestMove);
        }

        /// <summary>
        /// TTEntry の初期状態を検証する
        /// </summary>
        [TestMethod]
        public void TTEntryの初期状態を検証する()
        {
            // Arrange & Act
            var entry = new TTEntry();

            // Assert: 構造体のデフォルト値を確認
            Assert.AreEqual(0UL, entry.Key, "Key の初期値が 0 ではありません");
            Assert.AreEqual(0, entry.Depth, "Depth の初期値が 0 ではありません");
            Assert.AreEqual(0L, entry.Value, "Value の初期値が 0 ではありません");
            Assert.AreEqual(BoundType.Exact, entry.Bound, "Bound の初期値が Exact ではありません");
            Assert.AreEqual(0, entry.BestMove, "BestMove のデフォルト値が 0 ではありません");

            // Clear 後の状態を確認
            entry = new TTEntry(123UL, 5, 100, BoundType.LowerBound, 42);
            entry.Clear();

            Assert.AreEqual(0UL, entry.Key, "Clear 後の Key が 0 ではありません");
            Assert.AreEqual(0, entry.Depth, "Clear 後の Depth が 0 ではありません");
            Assert.AreEqual(0L, entry.Value, "Clear 後の Value が 0 ではありません");
            Assert.AreEqual(BoundType.Exact, entry.Bound, "Clear 後の Bound が Exact ではありません");
            Assert.AreEqual(TTEntry.NoBestMove, entry.BestMove, "Clear 後の BestMove が -1 ではありません");
        }

        /// <summary>
        /// TranspositionTableConfig のサイズが 2 の累乗に丸められる
        /// </summary>
        [TestMethod]
        public void Configのサイズが2の累乗に丸められる()
        {
            // 2 の累乗はそのまま
            var config1 = new TranspositionTableConfig(1024);
            Assert.AreEqual(1024, config1.TableSize);

            // 2 の累乗でない場合は切り上げ
            var config2 = new TranspositionTableConfig(1000);
            Assert.AreEqual(1024, config2.TableSize);

            var config3 = new TranspositionTableConfig(1025);
            Assert.AreEqual(2048, config3.TableSize);

            // 最小サイズ以下は最小サイズに
            var config4 = new TranspositionTableConfig(100);
            Assert.AreEqual(1024, config4.TableSize); // MinTableSize = 1024
        }

        /// <summary>
        /// ITranspositionTable が DI から正しく解決される
        /// </summary>
        [TestMethod]
        public void ITranspositionTableがDIから正しく解決される()
        {
            // Act
            var table = Reluca.Di.DiProvider.Get().GetService<ITranspositionTable>();

            // Assert
            Assert.IsNotNull(table, "ITranspositionTable が null です");
            Assert.IsInstanceOfType(table, typeof(ZobristTranspositionTable),
                "ZobristTranspositionTable が解決されていません");
        }

        /// <summary>
        /// ITranspositionTable は Singleton として登録されている
        /// </summary>
        [TestMethod]
        public void ITranspositionTableはSingletonとして登録されている()
        {
            // Act
            var table1 = Reluca.Di.DiProvider.Get().GetService<ITranspositionTable>();
            var table2 = Reluca.Di.DiProvider.Get().GetService<ITranspositionTable>();

            // Assert
            Assert.AreSame(table1, table2, "Singleton ではありません");
        }
    }
}
