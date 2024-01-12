using Microsoft.Extensions.DependencyInjection;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// BoardStringToContextConverterTestの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class StringToBoardContextConverterTest : BaseUnitTest<StringToBoardContextConverter>
    {
        [TestMethod]
        public void 盤の状態を変換できる()
        {
            var actual = Target.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 1, ResourceType.In)));
            var expected = new BoardContext
            {
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010
            };
            Assert.AreEqual(expected, actual);
        }
    }
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
}