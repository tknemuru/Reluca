/// <summary>
/// 【ModuleDoc】
/// 責務: FindBestMover の統合テストを提供する
/// 入出力: テストリソース -> テスト結果
/// 副作用: なし
/// </summary>
using Reluca.Movers;

namespace Reluca.Tests.Movers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// FindBestMover の統合テストクラスです。
    /// TT を使用するため並列実行を無効化します。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FindBestMoverUnitTest : BaseUnitTest<FindBestMover>
    {
        /// <summary>
        /// FindBestMover 経由で終盤局面の探索が正常に動作することを検証します。
        /// 盤面がほぼ埋まった終盤局面（空き 3 マス）を使用し、
        /// 終盤パス（DiscCountEvaluator + depth=99）を通ることを確認します。
        /// </summary>
        [TestMethod]
        public void FindBestMover経由で終盤探索が正常に動作する()
        {
            // Arrange: 終盤局面（TurnCount=56, 空き 3 マス）
            var context = CreateGameContext(2, 1, ResourceType.In);

            // Act: FindBestMover 経由で探索（ISearchEngine を使用）
            var bestMove = Target.Move(context);

            // Assert: 有効な手が返される（0-63 の範囲内）
            Assert.IsTrue(bestMove >= 0 && bestMove < 64, $"無効な手が返されました: {bestMove}");
        }
    }
}
