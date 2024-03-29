﻿using Reluca.Models;
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
    /// MobilityUpdaterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class MobilityUpdaterTest : BaseUnitTest<MobilityUpdater>
    {
        [TestMethod]
        public void 配置可能状態を更新できる()
        {
            var expecteds = CreateMultipleGameContexts(1, 1, ResourceType.Out);
            var actuals = CreateMultipleGameContexts(1, 1, ResourceType.In);
            for (var i = 0; i < expecteds.Count; i++)
            {
                Target.Update(actuals[i]);
                AssertEqualGameContext(expecteds[i], actuals[i]);
            }
        }

        [TestMethod]
        public void 繰り返し配置可能状態を更新できる()
        {
            var expecteds = CreateMultipleGameContexts(2, 1, ResourceType.Out);
            var actual = CreateGameContext(2, 1, ResourceType.In);
            Target.Update(actual);
            AssertEqualGameContext(expecteds[0], actual);
            actual.Turn = Disc.Color.White;
            Target.Update(actual);
            AssertEqualGameContext(expecteds[1], actual);
        }
    }
}
