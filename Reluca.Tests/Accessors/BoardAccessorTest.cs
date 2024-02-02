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
        /// <summary>
        /// テスト対象のクラス名
        /// </summary>
        private const string TargetName = "BoardAccessor";

        [TestMethod]
        public void 指定したインデックスの状態が取得できる()
        {
            var context = UnitTestHelper.CreateGameContext(TargetName, 1, 1, ResourceType.In);
            Assert.AreEqual(Board.Status.Empty, BoardAccessor.GetState(context, BoardAccessor.ToIndex("a1")));
            Assert.AreEqual(Board.Status.Mobility, BoardAccessor.GetState(context, BoardAccessor.ToIndex("d3")));
            Assert.AreEqual(Board.Status.Black, BoardAccessor.GetState(context, BoardAccessor.ToIndex("e4")));
            Assert.AreEqual(Board.Status.White, BoardAccessor.GetState(context, BoardAccessor.ToIndex("d4")));
        }

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

        [TestMethod]
        public void 盤に石を置ける()
        {
            var context = new BoardContext();
            BoardAccessor.SetDisc(context, Disc.Color.Black, BoardAccessor.ToIndex("h1"));
            BoardAccessor.SetDisc(context, Disc.Color.Black, BoardAccessor.ToIndex("d2"));
            BoardAccessor.SetDisc(context, Disc.Color.Black, BoardAccessor.ToIndex("h8"));

            BoardAccessor.SetDisc(context, Disc.Color.White, BoardAccessor.ToIndex("a1"));
            BoardAccessor.SetDisc(context, Disc.Color.White, BoardAccessor.ToIndex("e5"));
            BoardAccessor.SetDisc(context, Disc.Color.White, BoardAccessor.ToIndex("a8"));
        }

        [TestMethod]
        public void ターンを逆の色に変更できる()
        {
            var context = new GameContext();
            context.Turn = Disc.Color.Black;
            BoardAccessor.ChangeOppositeTurn(context);
            Assert.AreEqual(context.Turn, Disc.Color.White);
            BoardAccessor.ChangeOppositeTurn(context);
            Assert.AreEqual(context.Turn, Disc.Color.Black);
        }

        [TestMethod]
        public void 指定した色の石の数を算出できる()
        {
            var contexts = UnitTestHelper.CreateMultipleBoardContexts(TargetName, 2, 1, ResourceType.In);

            // 1
            var context = contexts[0];
            var count = BoardAccessor.GetDiscCount(context, Disc.Color.Black);
            Assert.AreEqual(2, count);
            count = BoardAccessor.GetDiscCount(context, Disc.Color.White);
            Assert.AreEqual(10, count);

            // 2
            context = contexts[1];
            count = BoardAccessor.GetDiscCount(context, Disc.Color.Black);
            Assert.AreEqual(10, count);
            count = BoardAccessor.GetDiscCount(context, Disc.Color.White);
            Assert.AreEqual(2, count);

            // 3
            context = contexts[2];
            count = BoardAccessor.GetDiscCount(context, Disc.Color.Black);
            Assert.AreEqual(0, count);
            count = BoardAccessor.GetDiscCount(context, Disc.Color.White);
            Assert.AreEqual(12, count);

            // 4
            context = contexts[3];
            count = BoardAccessor.GetDiscCount(context, Disc.Color.Black);
            Assert.AreEqual(12, count);
            count = BoardAccessor.GetDiscCount(context, Disc.Color.White);
            Assert.AreEqual(0, count);

            // 5
            context = contexts[4];
            count = BoardAccessor.GetDiscCount(context, Disc.Color.Black);
            Assert.AreEqual(0, count);
            count = BoardAccessor.GetDiscCount(context, Disc.Color.White);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void ゲーム状態をディープコピーできる()
        {
            var context = new GameContext
            {
                TurnCount = 5,
                Turn = Disc.Color.Black,
                Black = 10,
                White = 20
            };
            var _context = BoardAccessor.DeepCopy(context);
            Assert.AreEqual(context, _context);

            _context.TurnCount = 6;
            _context.Turn = Disc.Color.White;
            _context.Black = 11;
            _context.White = 21;
            Assert.AreNotEqual(context.TurnCount, _context.TurnCount);
            Assert.AreNotEqual(context.Turn, _context.Turn);
            Assert.AreNotEqual(context.Black, _context.Black);
            Assert.AreNotEqual(context.White, _context.White);
        }

        [TestMethod]
        public void 盤状態をディープコピーできる()
        {
            var context = new BoardContext
            {
                Black = 10,
                White = 20
            };
            var _context = BoardAccessor.DeepCopy(context);
            Assert.AreEqual(context, _context);

            _context.Black = 11;
            _context.White = 21;
            Assert.AreNotEqual(context.Black, _context.Black);
            Assert.AreNotEqual(context.White, _context.White);
        }

        [TestMethod]
        public void インデックスが盤上に収まる妥当な値であるかどうかを判定できる()
        {
            Assert.IsFalse(BoardAccessor.IsValidIndex(-1));
            Assert.IsTrue(BoardAccessor.IsValidIndex(0));
            Assert.IsTrue(BoardAccessor.IsValidIndex(63));
            Assert.IsFalse(BoardAccessor.IsValidIndex(64));
        }
    }
}
