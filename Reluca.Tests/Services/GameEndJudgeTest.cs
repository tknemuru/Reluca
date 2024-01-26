using Reluca.Services;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Services
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    /// <summary>
    /// GameEndJudgeの単体テスト機能を提供します。
    /// </summary>
    public class GameEndJudgeTest : BaseUnitTest<GameEndJudge>
    {
        [TestMethod]
        public void お互い指し手がない場合はゲーム終了と判定できる()
        {
            var context = CreateGameContext(1, 1, ResourceType.In);
            var acutual = Target.Execute(context);
            Assert.AreEqual(true, acutual);
        }

        [TestMethod]
        public void 自身の指し手が存在する場合はゲーム続行と判定できる()
        {
            var contexts = CreateMultipleGameContexts(2, 1, ResourceType.In);
            for (var i = 0; i < contexts.Count; i++)
            {
                var acutual = Target.Execute(contexts[i]);
                Assert.AreEqual(false, acutual);
            }
        }

        [TestMethod]
        public void 盤がすべて埋まっていたらゲーム終了と判定できる()
        {
            var context = CreateGameContext(3, 1, ResourceType.In);
            var acutual = Target.Execute(context);
            Assert.AreEqual(true, acutual);
        }
    }
}
