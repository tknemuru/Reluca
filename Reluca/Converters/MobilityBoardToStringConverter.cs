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
    /// 着手可能状態を含めた盤状態の文字列変換機能を提供します。
    /// </summary>
    public class MobilityBoardToStringConverter : IConvertible<GameContext, string>
    {
        /// <summary>
        /// 着手可能状態を含めた盤状態を文字列に変換します。
        /// </summary>
        /// <param name="input">着手可能状態を含めた盤状態</param>
        /// <returns>文字列に変換した着手可能状態を含めた盤状態</returns>
        public string Convert(GameContext input)
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
                    }
                    else if (0ul < (input.White & (1ul << (j + (i * Board.Length)))))
                    {
                        sb.Append(Board.Icon.White);
                    }
                    else if (0ul < (input.Mobility & (1ul << (j + (i * Board.Length)))))
                    {
                        sb.Append(Board.Icon.Mobility);
                    }
                    else
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
