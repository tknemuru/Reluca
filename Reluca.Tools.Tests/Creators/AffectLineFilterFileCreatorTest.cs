using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Tools.Creators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools.Tests.Creators
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// AffectLineFilterFileCreatorの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class AffectLineFilterFileCreatorTest : BaseUnitTest<AffectLineFilterFileCreator>
    {
        [TestInitialize]
        public void Init()
        {
            Target = new AffectLineFilterFileCreator();
        }

        [TestMethod]
        public void 上下左右存在する位置でラインが取得できる()
        {
            var converter = DiProvider.Get().GetService<StringToBoardContextConverter>();
            var expected = new List<ulong>
            {
                converter.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 1, Reluca.Tests.ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 2, Reluca.Tests.ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 3, Reluca.Tests.ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(GetResourcePath(1, 4, Reluca.Tests.ResourceType.Out))).Black
            };

            var actual = Target.Create(11);

            try
            {
                CollectionAssert.AreEqual(expected, actual);
            } catch (Exception ex)
            {
                var stringConverter = DiProvider.Get().GetService<BoardContextToStringConverter>();
                for (var i = 0; i < expected.Count; i++)
                {
                    var context = new BoardContext();
                    context.Black = expected[i];
                    Console.WriteLine("expected {0}", i);
                    Console.WriteLine(stringConverter.Convert(context));
                    context.Black = actual[i];
                    Console.WriteLine("actual {0}", i);
                    Console.WriteLine(stringConverter.Convert(context));
                }
#pragma warning disable CA2200 // スタック詳細を保持するために再度スローします
                throw ex;
#pragma warning restore CA2200 // スタック詳細を保持するために再度スローします
            }
        }
    }
}
