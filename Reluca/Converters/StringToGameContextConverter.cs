using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// ゲーム状態を示す文字列の変換機能を提供します。
    /// </summary>
    public class StringToGameContextConverter : IConvertible<IEnumerable<string>, GameContext>
    {
        /// <summary>
        /// 文字列をゲーム状態に変換します。
        /// </summary>
        /// <param name="input">ゲーム状態を示す文字列</param>
        /// <returns>ゲーム状態</returns>
        public GameContext Convert(IEnumerable<string> input)
        {
            var context = new GameContext();
            var startBoard = false;
            var endBoard = false;
            var boardStrs = new List<string>();

            foreach (var s in input)
            {
                var keyValue = s.Split(SimpleText.KeyValueSeparator);

                switch (keyValue[0])
                {
                    case SimpleText.Key.TurnCount:
                        context.TurnCount = int.Parse(keyValue[1]);
                        break;
                    case SimpleText.Key.Stage:
                        context.Stage = int.Parse(keyValue[1]);
                        break;
                    case SimpleText.Key.Turn:
                        if (keyValue[1] == Disc.ColorName.Black)
                        {
                            context.Turn = Disc.Color.Black;
                        }
                        if (keyValue[1] == Disc.ColorName.White)
                        {
                            context.Turn = Disc.Color.White;
                        }
                        break;
                    case SimpleText.Key.Move:
                        context.Move = BoardAccessor.ToIndex(keyValue[1]);
                        if (startBoard)
                        {
                            endBoard = true;
                        }
                        break;
                    case SimpleText.Key.Board:
                        startBoard = true;
                        break;
                    default:
                        if (!startBoard || endBoard)
                        {
                            throw new ArgumentException($"キーが不正です。{keyValue[0]}");
                        }
                        break;
                }
                if (startBoard && !endBoard)
                {
                    boardStrs.Add(keyValue[0]);
                }
            }
            if (startBoard)
            {
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
                var _context = DiProvider.Get().GetService<StringToMobilityBoardConverter>().Convert(boardStrs.Skip(1));
                context.Board = _context.Board;
                context.Mobility = _context.Mobility;
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
            }
            return context;
        }
    }
}
