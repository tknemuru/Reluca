using Reluca.Contexts;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Updaters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// InitializeUpdaterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class InitializeUpdaterTest : BaseUnitTest<InitializeUpdater>
    {
        [TestMethod]
        public void ゲーム状態の初期化ができる()
        {
            var expected = CreateGameContext(1, 1, ResourceType.Out);
            var actual = new GameContext();
            Target.Update(actual);
            Assert.AreEqual(expected, actual);
        }
    }
}
