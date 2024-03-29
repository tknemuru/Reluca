﻿using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Updaters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// MoveAndReverseUpdaterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class MoveAndReverseUpdaterTest : BaseUnitTest<MoveAndReverseUpdater>
    {
        [TestMethod]
        public void 指し手によって石を裏返すことができる()
        {
            var expecteds = CreateMultipleGameContexts(1, 1, ResourceType.Out);
            var actuals = CreateMultipleGameContexts(1, 1, ResourceType.In);
            for (var i = 0; i < expecteds.Count ; i++)
            {
                Target.Update(actuals[i]);
                AssertEqualGameContext(expecteds[i], actuals[i]);
            }
        }

        [TestMethod]
        public void 反対側の色ターンで指し手によって石を裏返すことができる()
        {
            var expecteds = CreateMultipleGameContexts(2, 1, ResourceType.Out);
            var actuals = CreateMultipleGameContexts(2, 1, ResourceType.In);
            for (var i = 0; i < expecteds.Count; i++)
            {
                Target.Update(actuals[i]);
                AssertEqualGameContext(expecteds[i], actuals[i]);
            }
        }

        [TestMethod]
        public void 自石が隣接している場合は裏返せない()
        {
            var expecteds = CreateMultipleGameContexts(3, 1, ResourceType.Out);
            var actuals = CreateMultipleGameContexts(3, 1, ResourceType.In);
            for (var i = 0; i < expecteds.Count; i++)
            {
                Target.Update(actuals[i]);
                AssertEqualGameContext(expecteds[i], actuals[i]);
            }
        }

        [TestMethod]
        public void 端の指し手が有効であるかを正しく判定できる()
        {
            var expecteds = CreateMultipleGameContexts(4, 1, ResourceType.Out);
            var actuals = CreateMultipleGameContexts(4, 1, ResourceType.In);
            for (var i = 0; i < expecteds.Count; i++)
            {
                Target.Update(actuals[i]);
                AssertEqualGameContext(expecteds[i], actuals[i]);
            }
        }
    }
}
