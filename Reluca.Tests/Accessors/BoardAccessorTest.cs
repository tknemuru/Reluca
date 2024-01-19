using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Accessors
{
    /// <summary>
    /// BoardAccessorの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class BoardAccessorTest
    {
        [TestMethod]
        public void 列インデックスが取得できる()
        {
            Assert.AreEqual(0, BoardAccessor.GetColumnIndex(0));
            Assert.AreEqual(7, BoardAccessor.GetColumnIndex(7));
            Assert.AreEqual(0, BoardAccessor.GetColumnIndex(8));
            Assert.AreEqual(2, BoardAccessor.GetColumnIndex(10));
            Assert.AreEqual(7, BoardAccessor.GetColumnIndex(63));
        }

        [TestMethod]
        public void 行インデックスが取得できる()
        {
            Assert.AreEqual(0, BoardAccessor.GetRowIndex(0));
            Assert.AreEqual(0, BoardAccessor.GetRowIndex(7));
            Assert.AreEqual(1, BoardAccessor.GetRowIndex(8));
            Assert.AreEqual(1, BoardAccessor.GetRowIndex(10));
            Assert.AreEqual(7, BoardAccessor.GetRowIndex(63));
        }

        [TestMethod]
        public void ターン色の石状態を取得できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 10ul,
                White = 20ul
            };

            Assert.AreEqual(10ul, BoardAccessor.GetTurnDiscs(context));
            context.Turn = Disc.Color.White;
            Assert.AreEqual(20ul, BoardAccessor.GetTurnDiscs(context));
        }

        [TestMethod]
        public void ターンと反対色の石状態を取得できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 10ul,
                White = 20ul
            };

            Assert.AreEqual(20ul, BoardAccessor.GetOppositeDiscs(context));
            context.Turn = Disc.Color.White;
            Assert.AreEqual(10ul, BoardAccessor.GetOppositeDiscs(context));
        }

        [TestMethod]
        public void ターン色の石状態を設定できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 10ul,
                White = 20ul
            };

            BoardAccessor.SetTurnDiscs(context, 30ul);
            Assert.AreEqual(30ul, context.Black);
            Assert.AreEqual(20ul, context.White);
        }

        [TestMethod]
        public void ターンと反対色の石状態を設定できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 10ul,
                White = 20ul
            };

            BoardAccessor.SetOppositeDiscs(context, 30ul);
            Assert.AreEqual(10ul, context.Black);
            Assert.AreEqual(30ul, context.White);
        }

        [TestMethod]
        public void 指定したインデックスに石が存在するかどうかを確認できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 0b100,
                White = 0b10000
            };

            Assert.IsFalse(BoardAccessor.ExistsDisc(context, 0));
            Assert.IsTrue(BoardAccessor.ExistsDisc(context, 2));
            Assert.IsTrue(BoardAccessor.ExistsDisc(context, 4));
            Assert.IsFalse(BoardAccessor.ExistsDisc(context, 63));
        }

        [TestMethod]
        public void 指定したインデックスに自石が存在するかどうかを確認できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 0b100,
                White = 0b10000
            };

            Assert.IsFalse(BoardAccessor.ExistsTurnDisc(context, 0));
            Assert.IsTrue(BoardAccessor.ExistsTurnDisc(context, 2));
            Assert.IsFalse(BoardAccessor.ExistsTurnDisc(context, 4));
            Assert.IsFalse(BoardAccessor.ExistsTurnDisc(context, 63));
        }

        [TestMethod]
        public void 指定したインデックスに他石が存在するかどうかを確認できる()
        {
            var context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 0b100,
                White = 0b10000
            };

            Assert.IsFalse(BoardAccessor.ExistsOppsositeDisc(context, 0));
            Assert.IsFalse(BoardAccessor.ExistsOppsositeDisc(context, 2));
            Assert.IsTrue(BoardAccessor.ExistsOppsositeDisc(context, 4));
            Assert.IsFalse(BoardAccessor.ExistsOppsositeDisc(context, 63));
        }

        [TestMethod]
        public void 位置を示す文字列をインデックスに変換できる()
        {
            // 正常形
            Assert.AreEqual(11, BoardAccessor.ToIndex("d2"));
            Assert.AreEqual(0, BoardAccessor.ToIndex("a1"));
            Assert.AreEqual(7, BoardAccessor.ToIndex("h1"));
            Assert.AreEqual(8, BoardAccessor.ToIndex("a2"));
            Assert.AreEqual(63, BoardAccessor.ToIndex("h8"));

            // 逆
            Assert.AreEqual(11, BoardAccessor.ToIndex("2d"));
            Assert.AreEqual(0, BoardAccessor.ToIndex("1a"));
            Assert.AreEqual(7, BoardAccessor.ToIndex("1h"));
            Assert.AreEqual(8, BoardAccessor.ToIndex("2a"));
            Assert.AreEqual(63, BoardAccessor.ToIndex("8h"));

            // 大文字
            Assert.AreEqual(11, BoardAccessor.ToIndex("D2"));
            Assert.AreEqual(0, BoardAccessor.ToIndex("A1"));
            Assert.AreEqual(7, BoardAccessor.ToIndex("H1"));
            Assert.AreEqual(8, BoardAccessor.ToIndex("A2"));
            Assert.AreEqual(63, BoardAccessor.ToIndex("H8"));

            // 全角小文字
            Assert.AreEqual(11, BoardAccessor.ToIndex("ｄ2"));
            Assert.AreEqual(11, BoardAccessor.ToIndex("2ｄ"));

            // 全角大文字
            Assert.AreEqual(11, BoardAccessor.ToIndex("Ｄ2"));
            Assert.AreEqual(11, BoardAccessor.ToIndex("2Ｄ"));

            // 全角数字
            Assert.AreEqual(11, BoardAccessor.ToIndex("d２"));
            Assert.AreEqual(11, BoardAccessor.ToIndex("２d"));
            Assert.AreEqual(11, BoardAccessor.ToIndex("Ｄ２"));
            Assert.AreEqual(11, BoardAccessor.ToIndex("２Ｄ"));
        }

        [TestMethod]
        public void 位置を示すインデックスを文字列に変換できる()
        {
            Assert.AreEqual("d2", BoardAccessor.ToPosition(11));
            Assert.AreEqual("a1", BoardAccessor.ToPosition(0));
            Assert.AreEqual("h1", BoardAccessor.ToPosition(7));
            Assert.AreEqual("a2", BoardAccessor.ToPosition(8));
            Assert.AreEqual("h8", BoardAccessor.ToPosition(63));
        }
    }
}
