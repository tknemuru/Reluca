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
using System.Text.RegularExpressions;
using Reluca.Models;

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
            return UnitTestHelper.GetResourcePath(Target.GetType().Name, index, childIndex, type, extension);
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
            return UnitTestHelper.CreateBoardContext(Target.GetType().Name, index, childIndex, type, extension);
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
            return UnitTestHelper.CreateGameContext(Target.GetType().Name, index, childIndex, type, extension);
        }

        /// <summary>
        /// リソースファイルから複数の盤状態を作成します。
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>盤状態のリスト</returns>
        protected List<GameContext> CreateMultipleGameContexts(int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return UnitTestHelper.CreateMultipleGameContexts(Target.GetType().Name, index, childIndex, type, extension);
        }

        /// <summary>
        /// ゲーム状態が期待通りであるかを検証します。
        /// </summary>
        /// <param name="expected">期待するゲーム状態</param>
        /// <param name="actual">実際のゲーム状態</param>
        protected void AssertEqualGameContext(GameContext expected, GameContext actual)
        {
            UnitTestHelper.AssertEqualGameContext(expected, actual);
        }
    }
}
