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
    /// BoardUpdaterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class BoardUpdaterTest : BaseUnitTest<BoardUpdater>
    {
        [TestMethod]
        public void 指し手によって石を裏返すことができる()
        {
            var expected = CreateGameContext(1, 1, ResourceType.Out);
            var actual = CreateGameContext(1, 1, ResourceType.In);
            Target.Update(actual);
            AssertEqualGameContext(expected, actual);
            Assert.AreEqual(expected, actual);
        }
    }
}
