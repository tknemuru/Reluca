using Microsoft.Extensions.DependencyInjection;
using Reluca.Converters;
using Reluca.Di;

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
            Target.Convert(GetResourcePath(1, 1, ResourceType.In));
        }
    }
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
}