using Reluca.Accessors;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Models;
using Reluca.Serchers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Serchers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
#pragma warning disable CS8604
    [TestClass]
    public class NegaMaxTest : BaseUnitTest<NegaMax>
    {
        [TestMethod]
        public void 探索が行える()
        {
            var context = CreateGameContext(1, 1, ResourceType.In);
            var orgContext = BoardAccessor.DeepCopy(context);
            var actual = Target.Search(context);
            Console.WriteLine($"最善手1:{BoardAccessor.ToPosition(actual)}");
            Console.WriteLine($"評価値1:{Target.Value}");
            Assert.IsTrue(actual > 0);
            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, context);

            context.Turn = Disc.Color.White;
            Target.Clear();
            orgContext = BoardAccessor.DeepCopy(context);
            actual = Target.Search(context);
            Console.WriteLine($"最善手2:{BoardAccessor.ToPosition(actual)}");
            Console.WriteLine($"評価値2:{Target.Value}");
            Assert.IsTrue(actual > 0);
            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, context);
        }

        [TestMethod]
        public void 完全読みでの探索が行える()
        {
            Target.Initialize(DiProvider.Get().GetService<DiscCountEvaluator>(), 99);
            var context = CreateGameContext(2, 1, ResourceType.In);
            var orgContext = BoardAccessor.DeepCopy(context);
            var actual = Target.Search(context);
            Console.WriteLine($"最善手1:{BoardAccessor.ToPosition(actual)}");
            Console.WriteLine($"評価値1:{Target.Value}");
            Assert.IsTrue(actual > 0);
            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, context);

            context.Turn = Disc.Color.White;
            Target.Clear();
            orgContext = BoardAccessor.DeepCopy(context);
            actual = Target.Search(context);
            Console.WriteLine($"最善手2:{BoardAccessor.ToPosition(actual)}");
            Console.WriteLine($"評価値2:{Target.Value}");
            Assert.IsTrue(actual > 0);
            // ゲーム状態は何も変わっていない
            AssertEqualGameContext(orgContext, context);
        }
    }
}
