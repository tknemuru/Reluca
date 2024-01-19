using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// BoardStringToContextConverterTestの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class MobilityBoardToStringConverterTest : BaseUnitTest<MobilityBoardToStringConverter>
    {
        [TestMethod]
        public void 盤の状態を変換できる()
        {
            var expected = IEnumerableHelper.IEnumerableToString(FileHelper.ReadTextLines(GetResourcePath(1, 1, ResourceType.Out)));

            var input = new GameContext
            {
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            var actual = Target.Convert(input);

            Assert.AreEqual(expected, actual);
        }
    }
}
