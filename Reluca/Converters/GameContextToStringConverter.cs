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

            // ターン数
            if (input.TurnCount > 0)
            {
                sb.AppendLine($"{SimpleText.Key.TurnCount}{SimpleText.KeyValueSeparator}{input.TurnCount}");
            }

            // ステージ
            if (input.Stage > 0)
            {
                sb.AppendLine($"{SimpleText.Key.Stage}{SimpleText.KeyValueSeparator}{input.Stage}");
            }

            // ターン
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

            // 指し手
            if (input.Move > 0)
            {
                sb.AppendLine($"{SimpleText.Key.Move}{SimpleText.KeyValueSeparator}{BoardAccessor.ToPosition(input.Move)}");
            }

            // 盤
            sb.AppendLine($"{SimpleText.Key.Board}{SimpleText.KeyValueSeparator}");
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
            sb.Append(DiProvider.Get().GetService<MobilityBoardToStringConverter>().Convert(input));
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
            return sb.ToString();
        }
    }
}
