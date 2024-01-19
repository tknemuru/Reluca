using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// BoardStringToContextConverterの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class BoardContextToStringConverterTest : BaseUnitTest<BoardContextToStringConverter>
    {
        [TestMethod]
        public void 盤の状態を変換できる()
        {
            var expected = IEnumerableHelper.IEnumerableToString(FileHelper.ReadTextLines(GetResourcePath(1, 1, ResourceType.Out)));

            var input = new BoardContext
            {
                Black = 0b00100010_00010001_10001000_01000100_00100010_00010001_10001000_01000100,
                White = 0b00010001_10001000_01000100_00100010_00010001_10001000_01000100_00100010
            };
            var actual = Target.Convert(input);

            Assert.AreEqual(expected, actual);
        }
    }
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
}