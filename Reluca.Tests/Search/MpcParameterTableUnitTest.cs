/// <summary>
/// 【ModuleDoc】
/// 責務: MpcParameterTable の単体テストを提供する
/// 入出力: テストケース → テスト結果
/// 副作用: なし
///
/// テスト方針:
/// - 各ステージ・カットペアでパラメータが正しく取得できること
/// - カットペア定義が仕様通りであること
/// - 範囲外のステージ・インデックスで null が返ること
/// </summary>
using Reluca.Search;

namespace Reluca.Tests.Search
{
    /// <summary>
    /// MpcParameterTable の単体テストクラスです。
    /// </summary>
    [TestClass]
    public class MpcParameterTableUnitTest
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private MpcParameterTable _target;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MpcParameterTableUnitTest()
        {
            _target = new MpcParameterTable();
        }

        /// <summary>
        /// カットペアが3組定義されていること
        /// </summary>
        [TestMethod]
        public void カットペアが3組定義されている()
        {
            // Assert
            Assert.AreEqual(3, _target.CutPairs.Count);
        }

        /// <summary>
        /// カットペアの浅い探索深さと深い探索深さが仕様通りであること
        /// </summary>
        [TestMethod]
        public void カットペアの深さが仕様通りである()
        {
            // Assert: Pair 1 (d'=2, d=6)
            Assert.AreEqual(2, _target.CutPairs[0].ShallowDepth);
            Assert.AreEqual(6, _target.CutPairs[0].DeepDepth);

            // Assert: Pair 2 (d'=4, d=10)
            Assert.AreEqual(4, _target.CutPairs[1].ShallowDepth);
            Assert.AreEqual(10, _target.CutPairs[1].DeepDepth);

            // Assert: Pair 3 (d'=6, d=14)
            Assert.AreEqual(6, _target.CutPairs[2].ShallowDepth);
            Assert.AreEqual(14, _target.CutPairs[2].DeepDepth);
        }

        /// <summary>
        /// z 値が 1.645（p=0.95）であること
        /// </summary>
        [TestMethod]
        public void z値が1_645であること()
        {
            // Assert
            Assert.AreEqual(1.645, _target.ZValue, 0.001);
        }

        /// <summary>
        /// 序盤ステージ（1〜5）の sigma が仕様通りであること
        /// </summary>
        [TestMethod]
        public void 序盤ステージのsigmaが仕様通りである()
        {
            for (int stage = 1; stage <= 5; stage++)
            {
                // Pair 1: sigma = 800
                var p0 = _target.GetParameters(stage, 0);
                Assert.IsNotNull(p0, $"ステージ{stage}のPair 0がnull");
                Assert.AreEqual(800.0, p0.Sigma, $"ステージ{stage}のPair 0のsigmaが不正");

                // Pair 2: sigma = 1200
                var p1 = _target.GetParameters(stage, 1);
                Assert.IsNotNull(p1, $"ステージ{stage}のPair 1がnull");
                Assert.AreEqual(1200.0, p1.Sigma, $"ステージ{stage}のPair 1のsigmaが不正");

                // Pair 3: sigma = 1600
                var p2 = _target.GetParameters(stage, 2);
                Assert.IsNotNull(p2, $"ステージ{stage}のPair 2がnull");
                Assert.AreEqual(1600.0, p2.Sigma, $"ステージ{stage}のPair 2のsigmaが不正");
            }
        }

        /// <summary>
        /// 中盤ステージ（6〜10）の sigma が仕様通りであること
        /// </summary>
        [TestMethod]
        public void 中盤ステージのsigmaが仕様通りである()
        {
            for (int stage = 6; stage <= 10; stage++)
            {
                var p0 = _target.GetParameters(stage, 0);
                Assert.IsNotNull(p0, $"ステージ{stage}のPair 0がnull");
                Assert.AreEqual(500.0, p0.Sigma, $"ステージ{stage}のPair 0のsigmaが不正");

                var p1 = _target.GetParameters(stage, 1);
                Assert.IsNotNull(p1, $"ステージ{stage}のPair 1がnull");
                Assert.AreEqual(800.0, p1.Sigma, $"ステージ{stage}のPair 1のsigmaが不正");

                var p2 = _target.GetParameters(stage, 2);
                Assert.IsNotNull(p2, $"ステージ{stage}のPair 2がnull");
                Assert.AreEqual(1100.0, p2.Sigma, $"ステージ{stage}のPair 2のsigmaが不正");
            }
        }

        /// <summary>
        /// 終盤ステージ（11〜15）の sigma が仕様通りであること
        /// </summary>
        [TestMethod]
        public void 終盤ステージのsigmaが仕様通りである()
        {
            for (int stage = 11; stage <= 15; stage++)
            {
                var p0 = _target.GetParameters(stage, 0);
                Assert.IsNotNull(p0, $"ステージ{stage}のPair 0がnull");
                Assert.AreEqual(300.0, p0.Sigma, $"ステージ{stage}のPair 0のsigmaが不正");

                var p1 = _target.GetParameters(stage, 1);
                Assert.IsNotNull(p1, $"ステージ{stage}のPair 1がnull");
                Assert.AreEqual(500.0, p1.Sigma, $"ステージ{stage}のPair 1のsigmaが不正");

                var p2 = _target.GetParameters(stage, 2);
                Assert.IsNotNull(p2, $"ステージ{stage}のPair 2がnull");
                Assert.AreEqual(700.0, p2.Sigma, $"ステージ{stage}のPair 2のsigmaが不正");
            }
        }

        /// <summary>
        /// 全ステージ共通で a=1.0, b=0.0 であること
        /// </summary>
        [TestMethod]
        public void 全ステージでaが1_0でbが0_0である()
        {
            for (int stage = 1; stage <= 15; stage++)
            {
                for (int pair = 0; pair < 3; pair++)
                {
                    var p = _target.GetParameters(stage, pair);
                    Assert.IsNotNull(p, $"ステージ{stage}のPair {pair}がnull");
                    Assert.AreEqual(1.0, p.A, $"ステージ{stage}のPair {pair}のaが不正");
                    Assert.AreEqual(0.0, p.B, $"ステージ{stage}のPair {pair}のbが不正");
                }
            }
        }

        /// <summary>
        /// 範囲外のステージで null が返ること
        /// </summary>
        [TestMethod]
        public void 範囲外のステージでnullが返る()
        {
            // Assert
            Assert.IsNull(_target.GetParameters(0, 0), "ステージ0でnullが返らない");
            Assert.IsNull(_target.GetParameters(16, 0), "ステージ16でnullが返らない");
            Assert.IsNull(_target.GetParameters(-1, 0), "ステージ-1でnullが返らない");
        }

        /// <summary>
        /// 範囲外のカットペアインデックスで null が返ること
        /// </summary>
        [TestMethod]
        public void 範囲外のカットペアインデックスでnullが返る()
        {
            // Assert
            Assert.IsNull(_target.GetParameters(1, -1), "インデックス-1でnullが返らない");
            Assert.IsNull(_target.GetParameters(1, 3), "インデックス3でnullが返らない");
        }
    }
}
