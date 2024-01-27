using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 単体テストに関する補助機能を提供します。
    /// </summary>
    public static class UnitTestHelper
    {
        /// <summary>
        /// リソースのパスを取得します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>リソースパス</returns>
        public static string GetResourcePath(string targetName, int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return $"../../../Resources/{targetName}/{index.ToString().PadLeft(3, '0')}-{childIndex.ToString().PadLeft(3, '0')}-{type.ToString().ToLower()}.{extension}";
        }

        /// <summary>
        /// リソースファイルから盤状態を作成します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>盤状態</returns>
        public static BoardContext CreateBoardContext(string targetName, int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return DiProvider.Get().GetService<StringToBoardContextConverter>().Convert(FileHelper.ReadTextLines(GetResourcePath(targetName, index, childIndex, type)));
        }

        /// <summary>
        /// リソースファイルからゲーム状態を作成します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>ゲーム状態</returns>
        public static GameContext CreateGameContext(string targetName, int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return DiProvider.Get().GetService<StringToGameContextConverter>().Convert(FileHelper.ReadTextLines(GetResourcePath(targetName, index, childIndex, type)));
        }

        /// <summary>
        /// リソースファイルから複数の盤状態を作成します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>盤状態のリスト</returns>
        public static List<BoardContext> CreateMultipleBoardContexts(string targetName, int index, int childIndex, ResourceType type, string extension = "txt")
        {
            var contexts = new List<BoardContext>();
            var lines = FileHelper.ReadTextLines(GetResourcePath(targetName, index, childIndex, type));
            var unit = new List<string>();
            foreach (var value in lines)
            {
                var val = value.Trim();
                if (value.Contains(SimpleText.ContextSeparator) || val == string.Empty)
                {
                    if (unit.Count > 0)
                    {
                        contexts.Add(DiProvider.Get().GetService<StringToBoardContextConverter>().Convert(unit));
                    }
                    unit.Clear();
                    continue;
                }
                unit.Add(val);
            }
            if (unit.Count > 0)
            {
                contexts.Add(DiProvider.Get().GetService<StringToBoardContextConverter>().Convert(unit));
            }
            return contexts;
        }

        /// <summary>
        /// リソースファイルから複数のゲーム状態を作成します。
        /// </summary>
        /// <param name="targetName">テスト対象クラス名</param>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        /// <returns>ゲーム状態のリスト</returns>
        public static List<GameContext> CreateMultipleGameContexts(string targetName, int index, int childIndex, ResourceType type, string extension = "txt")
        {
            var contexts = new List<GameContext>();
            var lines = FileHelper.ReadTextLines(GetResourcePath(targetName, index, childIndex, type));
            var unit = new List<string>();
            foreach (var value in lines)
            {
                var val = value.Trim();
                if (value.Contains(SimpleText.ContextSeparator) || val == string.Empty)
                {
                    if (unit.Count > 0)
                    {
                        contexts.Add(DiProvider.Get().GetService<StringToGameContextConverter>().Convert(unit));
                    }
                    unit.Clear();
                    continue;
                }
                unit.Add(val);
            }
            if (unit.Count > 0)
            {
                contexts.Add(DiProvider.Get().GetService<StringToGameContextConverter>().Convert(unit));
            }
            return contexts;
        }

        /// <summary>
        /// ゲーム状態が期待通りであるかを検証します。
        /// </summary>
        /// <param name="expected">期待するゲーム状態</param>
        /// <param name="actual">実際のゲーム状態</param>
        public static void AssertEqualGameContext(GameContext expected, GameContext actual)
        {
            var expectedStr = DiProvider.Get().GetService<GameContextToStringConverter>().Convert(expected);
            var acutualStr = DiProvider.Get().GetService<GameContextToStringConverter>().Convert(actual);
            Assert.AreEqual(expectedStr, acutualStr);
            // 念のため生の状態も検証しておく
            Assert.AreEqual(expected, actual);
        }
    }
}
