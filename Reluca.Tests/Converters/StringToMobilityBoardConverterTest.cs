using Microsoft.Extensions.DependencyInjection;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// StringToMobilityBoardConverterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class StringToMobilityBoardConverterTest : BaseUnitTest<StringToMobilityBoardConverter>
    {
        [TestMethod]
        public void 盤の状態を変換できる()
        {
            var actual = Target.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 1, ResourceType.In)));
            var expected = new GameContext
            {
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010,
                Mobility = 0b01000100_00100010_00010001_10001000_01000100_00100010_00010001_10001000
            };
            Assert.AreEqual(expected, actual);
        }
    }
}
