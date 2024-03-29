﻿using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools.Tests
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
#pragma warning disable CS8618

    /// <summary>
    /// AffectLineFilterFileCreatorの単体テスト機能を提供します。
    /// </summary>
    [TestClass]
    public class AffectLineFilterFileCreatorTest
    {
        /// <summary>
        /// テスト対象のクラス名
        /// </summary>
        private const string TargetName = "AffectLineFilterFileCreator";

        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        private AffectLineFilterFileCreator Target { get; set; }

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
                converter.Convert(FileHelper.ReadTextLines(UnitTestHelper.GetResourcePath(TargetName, 1, 1, ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(UnitTestHelper.GetResourcePath(TargetName, 1, 2, ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(UnitTestHelper.GetResourcePath(TargetName, 1, 3, ResourceType.Out))).Black,
                converter.Convert(FileHelper.ReadTextLines(UnitTestHelper.GetResourcePath(TargetName, 1, 4, ResourceType.Out))).Black
            };

            var actual = Target.Create(11);

            try
            {
                CollectionAssert.AreEqual(expected, actual);
            }
            catch (Exception ex)
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
