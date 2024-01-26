using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// GameContextToStringConverterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class GameContextToStringConverterTest : BaseUnitTest<GameContextToStringConverter>
    {
        [TestMethod]
        public void ゲーム状態を変換できる()
        {
            var expectedRows = FileHelper.ReadTextLines(GetResourcePath(1, 1, ResourceType.Out));
            var expected = new StringBuilder();
            foreach(var row in expectedRows)
            {
                expected.AppendLine(row);
            }
            var context = new GameContext
            {
                TurnCount = 5,
                Stage = 3,
                Turn = Disc.Color.Black,
                Move = 35,
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            var actual = Target.Convert(context);
            Assert.AreEqual(expected.ToString(), actual);
        }

        [TestMethod]
        public void 一部情報が欠落していてもゲーム状態を変換できる()
        {
            // 001
            var expectedRows = FileHelper.ReadTextLines(GetResourcePath(2, 1, ResourceType.Out));
            var expected = new StringBuilder();
            foreach (var row in expectedRows)
            {
                expected.AppendLine(row);
            }
            var context = new GameContext
            {
                Move = 35,
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            var actual = Target.Convert(context);
            Assert.AreEqual(expected.ToString(), actual);

            // 002
            expectedRows = FileHelper.ReadTextLines(GetResourcePath(2, 2, ResourceType.Out));
            expected = new StringBuilder();
            foreach (var row in expectedRows)
            {
                expected.AppendLine(row);
            }
            context = new GameContext
            {
                Turn = Disc.Color.Black,
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            actual = Target.Convert(context);
            Assert.AreEqual(expected.ToString(), actual);

            // 003
            expectedRows = FileHelper.ReadTextLines(GetResourcePath(2, 3, ResourceType.Out));
            expected = new StringBuilder();
            foreach (var row in expectedRows)
            {
                expected.AppendLine(row);
            }
            context = new GameContext
            {
                Turn = Disc.Color.Black,
                Move = 35
            };
            actual = Target.Convert(context);
            Assert.AreEqual(expected.ToString(), actual);
        }
    }
}
