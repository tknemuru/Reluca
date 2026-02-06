/// <summary>
/// 【ModuleDoc】
/// 責務: AspirationParameterTable の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - 各ステージで期待する delta 値が返されること
/// - 範囲外ステージでデフォルト delta が返ること
/// - 深さ補正が整数演算で正確に計算されること
/// </summary>
using Reluca.Search;

namespace Reluca.Tests.Search
{
    /// <summary>
    /// AspirationParameterTable の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class AspirationParameterTableUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private AspirationParameterTable _target;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AspirationParameterTableUnitTest()
        {
            _target = new AspirationParameterTable();
        }

        /// <summary>
        /// デフォルト delta が 50 であること
        /// </summary>
        [TestMethod]
        public void デフォルトDeltaが50である()
        {
            // Assert
            Assert.AreEqual(50, _target.DefaultDelta);
        }

        /// <summary>
        /// 序盤ステージ（1〜5）で delta が 80 であること
        /// </summary>
        [TestMethod]
        public void 序盤ステージのdeltaが80である()
        {
            for (int stage = 1; stage <= 5; stage++)
            {
                // Assert
                Assert.AreEqual(80, _target.GetDelta(stage),
                    $"ステージ{stage}の delta が 80 ではありません");
            }
        }

        /// <summary>
        /// 中盤ステージ（6〜10）で delta が 50 であること
        /// </summary>
        [TestMethod]
        public void 中盤ステージのdeltaが50である()
        {
            for (int stage = 6; stage <= 10; stage++)
            {
                // Assert
                Assert.AreEqual(50, _target.GetDelta(stage),
                    $"ステージ{stage}の delta が 50 ではありません");
            }
        }

        /// <summary>
        /// 終盤ステージ（11〜15）で delta が 30 であること
        /// </summary>
        [TestMethod]
        public void 終盤ステージのdeltaが30である()
        {
            for (int stage = 11; stage <= 15; stage++)
            {
                // Assert
                Assert.AreEqual(30, _target.GetDelta(stage),
                    $"ステージ{stage}の delta が 30 ではありません");
            }
        }

        /// <summary>
        /// 範囲外ステージ（0）でデフォルト delta が返ること
        /// </summary>
        [TestMethod]
        public void ステージ0でデフォルトDeltaが返る()
        {
            // Assert
            Assert.AreEqual(50, _target.GetDelta(0),
                "ステージ 0 でデフォルト delta が返りません");
        }

        /// <summary>
        /// 範囲外ステージ（16）でデフォルト delta が返ること
        /// </summary>
        [TestMethod]
        public void ステージ16でデフォルトDeltaが返る()
        {
            // Assert
            Assert.AreEqual(50, _target.GetDelta(16),
                "ステージ 16 でデフォルト delta が返りません");
        }

        /// <summary>
        /// 範囲外ステージ（-1）でデフォルト delta が返ること
        /// </summary>
        [TestMethod]
        public void 負のステージでデフォルトDeltaが返る()
        {
            // Assert
            Assert.AreEqual(50, _target.GetDelta(-1),
                "ステージ -1 でデフォルト delta が返りません");
        }

        /// <summary>
        /// 深さ補正: depth <= 2 で 2.0 倍
        /// </summary>
        [TestMethod]
        public void 深さ補正_depth2で2倍()
        {
            // Assert: GetAdjustedDelta(30, 2) = 60
            Assert.AreEqual(60, AspirationParameterTable.GetAdjustedDelta(30, 2));
            // Assert: GetAdjustedDelta(80, 1) = 160
            Assert.AreEqual(160, AspirationParameterTable.GetAdjustedDelta(80, 1));
            // Assert: GetAdjustedDelta(50, 2) = 100
            Assert.AreEqual(100, AspirationParameterTable.GetAdjustedDelta(50, 2));
        }

        /// <summary>
        /// 深さ補正: depth 3〜4 で 1.5 倍（整数演算の切り捨て確認）
        /// </summary>
        [TestMethod]
        public void 深さ補正_depth4で1_5倍()
        {
            // Assert: GetAdjustedDelta(31, 4) = 46 （31 * 3 / 2 = 46、整数除算で切り捨て）
            Assert.AreEqual(46, AspirationParameterTable.GetAdjustedDelta(31, 4));
            // Assert: GetAdjustedDelta(30, 3) = 45
            Assert.AreEqual(45, AspirationParameterTable.GetAdjustedDelta(30, 3));
            // Assert: GetAdjustedDelta(50, 4) = 75
            Assert.AreEqual(75, AspirationParameterTable.GetAdjustedDelta(50, 4));
            // Assert: GetAdjustedDelta(80, 3) = 120
            Assert.AreEqual(120, AspirationParameterTable.GetAdjustedDelta(80, 3));
        }

        /// <summary>
        /// 深さ補正: depth > 4 で補正なし（1.0 倍）
        /// </summary>
        [TestMethod]
        public void 深さ補正_depth5以上で補正なし()
        {
            // Assert: GetAdjustedDelta(50, 5) = 50
            Assert.AreEqual(50, AspirationParameterTable.GetAdjustedDelta(50, 5));
            // Assert: GetAdjustedDelta(80, 7) = 80
            Assert.AreEqual(80, AspirationParameterTable.GetAdjustedDelta(80, 7));
            // Assert: GetAdjustedDelta(30, 10) = 30
            Assert.AreEqual(30, AspirationParameterTable.GetAdjustedDelta(30, 10));
        }
    }
}
