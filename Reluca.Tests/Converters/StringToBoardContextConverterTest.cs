using Microsoft.Extensions.DependencyInjection;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;

namespace Reluca.Tests.Converters
{
#pragma warning disable CS8602 // null �Q�Ƃ̉\����������̂̋t�Q�Ƃł��B
    /// <summary>
    /// BoardStringToContextConverterTest�̒P�̃e�X�g�@�\��񋟂��܂��B
    /// </summary>
    [TestClass]
    public class StringToBoardContextConverterTest : BaseUnitTest<StringToBoardContextConverter>
    {
        [TestMethod]
        public void �Ղ̏�Ԃ�ϊ��ł���()
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
#pragma warning restore CS8602 // null �Q�Ƃ̉\����������̂̋t�Q�Ƃł��B
}