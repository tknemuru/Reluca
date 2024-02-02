using Reluca.Accessors;
using Reluca.Movers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Movers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    public class FindFirstMoverTest : BaseUnitTest<FindFirstMover>
    {
        [TestMethod]
        public void 初めに見つけた有効な指し手が取得できる()
        {
            var context = CreateGameContext(1, 1, ResourceType.In);
            var move = Target.Move(context);
            Assert.AreEqual(BoardAccessor.ToIndex("d3"), move);
        }
    }
}
