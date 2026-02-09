/// <summary>
/// 【ModuleDoc】
/// 責務: TimeAllocator の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - 各フェーズ（序盤・中盤・終盤）で適切な配分比率が適用されることを確認
/// - 最低保証時間（100ms）が常に確保されることを確認
/// - 残り時間が 0 以下の場合に最低保証時間が返されることを確認
/// - 中盤の配分が序盤より大きいことを確認
/// </summary>
using Reluca.Search;

namespace Reluca.Tests.Search
{
    /// <summary>
    /// TimeAllocator の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class TimeAllocatorUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private TimeAllocator Target { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TimeAllocatorUnitTest()
        {
            Target = new TimeAllocator();
        }

        /// <summary>
        /// 序盤（ターン 0）で正の配分が返される
        /// </summary>
        [TestMethod]
        public void 序盤で正の配分が返される()
        {
            // Arrange
            long remainingTimeMs = 300000; // 5 分
            int turnCount = 0;

            // Act
            long result = Target.Allocate(remainingTimeMs, turnCount);

            // Assert
            Assert.IsTrue(result > 0, $"配分が 0 以下です: {result}");
            Assert.IsTrue(result >= 100, $"最低保証時間（100ms）を下回っています: {result}");
            Console.WriteLine($"序盤 (turn={turnCount}): {result}ms");
        }

        /// <summary>
        /// 中盤（ターン 30）で正の配分が返される
        /// </summary>
        [TestMethod]
        public void 中盤で正の配分が返される()
        {
            // Arrange
            long remainingTimeMs = 200000; // 残り約 3.3 分
            int turnCount = 30;

            // Act
            long result = Target.Allocate(remainingTimeMs, turnCount);

            // Assert
            Assert.IsTrue(result > 0, $"配分が 0 以下です: {result}");
            Assert.IsTrue(result >= 100, $"最低保証時間（100ms）を下回っています: {result}");
            Console.WriteLine($"中盤 (turn={turnCount}): {result}ms");
        }

        /// <summary>
        /// 終盤（ターン 50）で正の配分が返される
        /// </summary>
        [TestMethod]
        public void 終盤で正の配分が返される()
        {
            // Arrange
            long remainingTimeMs = 50000; // 残り 50 秒
            int turnCount = 50;

            // Act
            long result = Target.Allocate(remainingTimeMs, turnCount);

            // Assert
            Assert.IsTrue(result > 0, $"配分が 0 以下です: {result}");
            Assert.IsTrue(result >= 100, $"最低保証時間（100ms）を下回っています: {result}");
            Console.WriteLine($"終盤 (turn={turnCount}): {result}ms");
        }

        /// <summary>
        /// 中盤の配分が序盤の配分より大きい
        /// </summary>
        [TestMethod]
        public void 中盤の配分が序盤の配分より大きい()
        {
            // Arrange: 同じ残り時間で序盤と中盤を比較
            long remainingTimeMs = 300000; // 5 分

            // Act
            long earlyAlloc = Target.Allocate(remainingTimeMs, 10);  // 序盤
            long midAlloc = Target.Allocate(remainingTimeMs, 30);    // 中盤

            // Assert: 中盤の方が大きい配分
            // 残り手数の差も考慮（中盤は残り手数が少ないため基本配分が大きい + フェーズ係数 1.3）
            Assert.IsTrue(midAlloc > earlyAlloc,
                $"中盤の配分（{midAlloc}ms）が序盤の配分（{earlyAlloc}ms）以下です");
            Console.WriteLine($"序盤={earlyAlloc}ms, 中盤={midAlloc}ms");
        }

        /// <summary>
        /// 残り時間が 0 以下の場合に最低保証時間が返される
        /// </summary>
        [TestMethod]
        public void 残り時間が0以下の場合に最低保証時間が返される()
        {
            // Act & Assert: 0ms
            Assert.AreEqual(100, Target.Allocate(0, 30),
                "残り時間 0 で最低保証時間が返されません");

            // Act & Assert: 負値
            Assert.AreEqual(100, Target.Allocate(-1000, 30),
                "残り時間が負値で最低保証時間が返されません");
        }

        /// <summary>
        /// 残り時間が極小の場合でも最低保証時間が返される
        /// </summary>
        [TestMethod]
        public void 残り時間が極小の場合でも最低保証時間が返される()
        {
            // Arrange: 残り 1ms
            long result = Target.Allocate(1, 30);

            // Assert: 最低 100ms が保証される
            Assert.AreEqual(100, result,
                $"極小の残り時間で最低保証時間が返されません: {result}");
        }

        /// <summary>
        /// ターン 59（最終手近く）で正の配分が返される
        /// </summary>
        [TestMethod]
        public void 最終手近くで正の配分が返される()
        {
            // Arrange
            long remainingTimeMs = 10000; // 残り 10 秒
            int turnCount = 59;

            // Act
            long result = Target.Allocate(remainingTimeMs, turnCount);

            // Assert
            Assert.IsTrue(result >= 100, $"最低保証時間を下回っています: {result}");
            Console.WriteLine($"最終手近く (turn={turnCount}): {result}ms");
        }

        /// <summary>
        /// フェーズ境界のターン数で正しく動作する
        /// </summary>
        [TestMethod]
        public void フェーズ境界で正しく動作する()
        {
            // Arrange
            long remainingTimeMs = 300000;

            // Act: 各フェーズ境界で配分を計算
            long turn15 = Target.Allocate(remainingTimeMs, 15); // 序盤の最後
            long turn16 = Target.Allocate(remainingTimeMs, 16); // 中盤の最初
            long turn44 = Target.Allocate(remainingTimeMs, 44); // 中盤の最後
            long turn45 = Target.Allocate(remainingTimeMs, 45); // 終盤の最初

            // Assert: 中盤開始で配分が増加する
            Assert.IsTrue(turn16 > turn15,
                $"ターン 16 の配分（{turn16}ms）がターン 15 の配分（{turn15}ms）以下です");

            Console.WriteLine($"turn15={turn15}ms, turn16={turn16}ms, turn44={turn44}ms, turn45={turn45}ms");
        }

        /// <summary>
        /// 長時間対局（60 分）で妥当な配分が返される
        /// </summary>
        [TestMethod]
        public void 長時間対局で妥当な配分が返される()
        {
            // Arrange: 60 分 = 3,600,000ms
            long remainingTimeMs = 3600000;
            int turnCount = 0;

            // Act
            long result = Target.Allocate(remainingTimeMs, turnCount);

            // Assert: 合理的な範囲の配分
            Assert.IsTrue(result > 0, $"配分が 0 以下です: {result}");
            // 60 分 / 30 手 * 0.95 * 0.8 ≈ 91,200ms が目安
            Assert.IsTrue(result < remainingTimeMs,
                $"配分が残り時間を超えています: {result}");
            Console.WriteLine($"長時間対局 (turn={turnCount}): {result}ms");
        }
    }
}
