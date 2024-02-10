using Reluca.Accessors;
using Reluca.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools.Tests
{
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    [TestClass]
    public class FeaturePatternCreatorTest
    {
        /// <summary>
        /// テスト対象のクラス名
        /// </summary>
        private const string TargetName = "FeaturePatternCreator";

        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private FeaturePatternCreator Target { get; set; }

        [TestInitialize]
        public void Init()
        {
            Target = new FeaturePatternCreator();
        }

        [TestMethod]
        public void 盤状態文字列からインデックスのリストが作成できる()
        {
            // 001
            var actual = FeaturePatternCreator.Convert(UnitTestHelper.ReadResource(TargetName, 1, 1, ResourceType.In)).ToArray();
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("d1"), actual[0]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("c2"), actual[1]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("b3"), actual[2]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("a4"), actual[3]);

            // 002
            actual = FeaturePatternCreator.Convert(UnitTestHelper.ReadResource(TargetName, 1, 2, ResourceType.In)).ToArray();
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("d8"), actual[0]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("c7"), actual[1]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("b6"), actual[2]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("a5"), actual[3]);
        }

        [TestMethod]
        public void 盤状態文字列から特徴パターンの辞書が作成できる()
        {
            // 001
            var actual = FeaturePatternCreator.Create(UnitTestHelper.ReadResource(TargetName, 2, 1, ResourceType.In));
            var diag41 = actual["diag4"].ToArray()[0];
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("d1"), diag41[0]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("c2"), diag41[1]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("b3"), diag41[2]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("a4"), diag41[3]);
            var diag42 = actual["diag4"].ToArray()[1];
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("d8"), diag42[0]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("c7"), diag42[1]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("b6"), diag42[2]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("a5"), diag42[3]);
            var horVert2 = actual["hor_vert2"].ToArray()[0];
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("a2"), horVert2[0]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("b2"), horVert2[1]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("c2"), horVert2[2]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("d2"), horVert2[3]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("e2"), horVert2[4]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("f2"), horVert2[5]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("g2"), horVert2[6]);
            Assert.AreEqual(1ul << BoardAccessor.ToIndex("h2"), horVert2[7]);
        }
    }
}
