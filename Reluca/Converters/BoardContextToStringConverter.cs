using Reluca.Di;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reluca.Contexts;

namespace Reluca.Converters
{
    /// <summary>
    /// 盤コンテキストの文字列変換機能を提供します。
    /// </summary>
    public class BoardContextToStringConverter : IConvertible<BoardContext, string>
    {
        /// <summary>
        /// 盤コンテキストを文字列に変換します。
        /// </summary>
        /// <param name="input">盤コンテキスト</param>
        /// <returns>文字列に変換した盤コンテキスト</returns>
        public string Convert(BoardContext input)
        {
            var gameContext = new GameContext(input);
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
            return DiProvider.Get().GetService<MobilityBoardToStringConverter>().Convert(gameContext);
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
        }
    }
}
