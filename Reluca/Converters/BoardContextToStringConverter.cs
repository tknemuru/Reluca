using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        /// <returns>文字列に変化した盤コンテキスト</returns>
        public string Convert(BoardContext input)
        {
            Debug.Assert(input != null);
            var sb = new StringBuilder();
            sb.AppendLine("　ａｂｃｄｅｆｇｈ　");
            for (var i = 0; i < Board.Length; i++)
            {
                var wideIdx = Regex.Replace((i + 1).ToString(), "[0-9]", si => ((char)(si.Value[0] - '0' + '０')).ToString());
                sb.Append(wideIdx);
                for (var j = 0; j < Board.Length; j++)
                {
                    if (0ul < (input.Black & (1ul << (j + (i * Board.Length)))))
                    {
                        sb.Append(Board.Icon.Black);
                    } else if (0ul < (input.White & (1ul << (j + (i * Board.Length)))))
                    {
                        sb.Append(Board.Icon.White);
                    } else
                    {
                        sb.Append(Board.Icon.Empty);
                    }
                }
                sb.AppendLine(wideIdx);
            }
            sb.AppendLine("　ａｂｃｄｅｆｇｈ　");
            return sb.ToString();
        }
    }
}
