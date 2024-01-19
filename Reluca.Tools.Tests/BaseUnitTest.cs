using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;
using Reluca.Di;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Reluca.Tests;

namespace Reluca.Tools.Tests
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 基底ユニットテストクラス
    /// </summary>
    public abstract class BaseUnitTest<T> where T : class
    {
        /// <summary>
        /// テスト対象のインスタンス
        /// </summary>
        /// <value>The target.</value>
        protected T? Target { get; set; }

        /// <summary>
        /// リソースのパスを取得します。
        /// </summary>
        /// <returns>リソースパス</returns>
        /// <param name="index">インデックス</param>
        /// <param name="childIndex">子インデックス</param>
        /// <param name="type">リソース種別</param>
        /// <param name="extension">拡張子</param>
        protected string GetResourcePath(int index, int childIndex, ResourceType type, string extension = "txt")
        {
            return $"../../../Resources/{Target.GetType().Name}/{index.ToString().PadLeft(3, '0')}-{childIndex.ToString().PadLeft(3, '0')}-{type.ToString().ToLower()}.{extension}";
        }
    }
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
}
