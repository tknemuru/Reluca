using Reluca.Di;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Reluca.Helpers;
using Reluca.Converters;
using Reluca.Contexts;

namespace Reluca.Tests
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 基底ユニットテストクラス
    /// </summary>
    [TestClass]
    public abstract class BaseUnitTest<T> where T : class
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        /// <value>The target.</value>
        protected T? Target { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        protected BaseUnitTest()
        {
            Target = DiProvider.Get().GetService<T>();
            Debug.Assert(Target != null);
        }

        /// <summary>
        /// リソースのパスを取得します。
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>リソースパス</returns>
        protected string GetResourcePath(int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return $"../../../Resources/{Target.GetType().Name}/{index.ToString().PadLeft(3, '0')}-{childIndex.ToString().PadLeft(3, '0')}-{type.ToString().ToLower()}.{extension}";
        }

        /// <summary>
        /// リソースファイルから盤状態を作成します。
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>盤状態</returns>
        protected BoardContext CreateBoardContext(int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return DiProvider.Get().GetService<StringToBoardContextConverter>().Convert(FileHelper.ReadTextLines(GetResourcePath(index, childIndex, type)));
        }

        /// <summary>
        /// リソースファイルから盤状態を作成します。
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>盤状態</returns>
        protected GameContext CreateGameContext(int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return DiProvider.Get().GetService<StringToGameContextConverter>().Convert(FileHelper.ReadTextLines(GetResourcePath(index, childIndex, type)));
        }

        /// <summary>
        /// ゲーム状態が期待通りであるかを検証します。
        /// </summary>
        /// <param name="expected">期待するゲーム状態</param>
        /// <param name="actual">実際のゲーム状態</param>
        protected void AssertEqualGameContext(GameContext expected, GameContext actual)
        {
            var expectedStr = DiProvider.Get().GetService<GameContextToStringConverter>().Convert(expected);
            var acutualStr = DiProvider.Get().GetService<GameContextToStringConverter>().Convert(actual);
            Assert.AreEqual(expectedStr, acutualStr);
        }
    }
}
