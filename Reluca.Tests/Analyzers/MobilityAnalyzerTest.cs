using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Analyzers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    [TestClass]
    public class MobilityAnalyzerTest : BaseUnitTest<MobilityAnalyzer>
    {
        [TestMethod]
        public void 着手可能数が取得できる()
        {
            var inputs = CreateMultipleGameContexts(1, 1, ResourceType.In);

            // ターン未指定
            var expected = new List<int>()
            {
                BoardAccessor.ToIndex("d3"),
                BoardAccessor.ToIndex("c4"),
                BoardAccessor.ToIndex("f5"),
                BoardAccessor.ToIndex("e6"),
            };
            var actual = Target.Analyze(inputs[0]);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("b5"),
            };
            actual = Target.Analyze(inputs[1]);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("f1"),
            };
            actual = Target.Analyze(inputs[2]);
            CollectionAssert.AreEqual(expected, actual);

            // ターン白指定
            expected = new List<int>()
            {
                BoardAccessor.ToIndex("e3"),
                BoardAccessor.ToIndex("f4"),
                BoardAccessor.ToIndex("c5"),
                BoardAccessor.ToIndex("d6"),
            };
            actual = Target.Analyze(inputs[0], Disc.Color.White);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("e8"),
            };
            actual = Target.Analyze(inputs[1], Disc.Color.White);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("b1"),
                BoardAccessor.ToIndex("f3"),
            };
            actual = Target.Analyze(inputs[2], Disc.Color.White);
            CollectionAssert.AreEqual(expected, actual);

            // ターン黒指定
            expected = new List<int>()
            {
                BoardAccessor.ToIndex("d3"),
                BoardAccessor.ToIndex("c4"),
                BoardAccessor.ToIndex("f5"),
                BoardAccessor.ToIndex("e6"),
            };
            actual = Target.Analyze(inputs[0]);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("b5"),
            };
            actual = Target.Analyze(inputs[1]);
            CollectionAssert.AreEqual(expected, actual);

            expected = new List<int>()
            {
                BoardAccessor.ToIndex("f1"),
            };
            actual = Target.Analyze(inputs[2]);
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
