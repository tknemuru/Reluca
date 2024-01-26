using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// ゲーム状態を文字列に変換する機能を提供します。
    /// </summary>
    public class GameContextToStringConverter : IConvertible<GameContext, string>
    {
        /// <summary>
        /// ゲーム状態を文字列に変換します。
        /// </summary>
        /// <param name="input">ゲーム状態</param>
        /// <returns>ゲーム状態を示す文字列</returns>
        public string Convert(GameContext input)
        {
            var sb = new StringBuilder();
            var turn = string.Empty;
            if (input.Turn == Disc.Color.Black)
            {
                turn = Disc.ColorName.Black;
            }
            if (input.Turn == Disc.Color.White)
            {
                turn = Disc.ColorName.White;
            }
            if (turn != string.Empty)
            {
                sb.AppendLine($"{SimpleText.Key.Turn}{SimpleText.KeyValueSeparator}{turn}");
            }
            if (input.Move > 0)
            {
                sb.AppendLine($"{SimpleText.Key.Move}{SimpleText.KeyValueSeparator}{BoardAccessor.ToPosition(input.Move)}");
            }
            sb.AppendLine($"{SimpleText.Key.Board}{SimpleText.KeyValueSeparator}");
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
            sb.Append(DiProvider.Get().GetService<MobilityBoardToStringConverter>().Convert(input));
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
            return sb.ToString();
        }
    }
}
