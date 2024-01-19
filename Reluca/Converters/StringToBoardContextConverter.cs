using Microsoft.Extensions.DependencyInjection;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 文字列を盤コンテキストに変換する機能を提供します。
    /// </summary>
    public class StringToBoardContextConverter : IConvertible<IEnumerable<string>, BoardContext>
    {
        /// <summary>
        /// 文字列を盤コンテキストに変換します。
        /// </summary>
        /// <param name="input">盤コンテキストの文字列</param>
        /// <returns>盤コンテキスト</returns>
        public BoardContext Convert(IEnumerable<string> input)
        {
            var converter = DiProvider.Get().GetService<StringToMobilityBoardConverter>();
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
            return converter.Convert(input).Board;
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
        }
    }
}
