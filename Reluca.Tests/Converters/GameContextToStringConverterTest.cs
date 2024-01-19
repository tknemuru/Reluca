﻿using Reluca.Contexts;
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
                Turn = Disc.Color.Black,
                Move = 35,
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            var actual = Target.Convert(context);
            Assert.AreEqual(expected.ToString(), actual);
        }
    }
}
