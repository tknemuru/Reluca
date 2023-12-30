using Microsoft.Extensions.DependencyInjection;
using Reluca.Converters;
using Reluca.Di;

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
            Target.Convert(GetResourcePath(1, 1, ResourceType.In));
        }
    }
#pragma warning restore CS8602 // null �Q�Ƃ̉\����������̂̋t�Q�Ƃł��B
}