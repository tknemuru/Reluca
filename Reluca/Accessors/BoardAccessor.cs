using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Reluca.Accessors
{
    /// <summary>
    /// 盤に対する操作機能を提供します。
    /// </summary>
    public static class BoardAccessor
    {
        /// <summary>
        /// 列の位置を示す文字列
        /// </summary>
        private const string ColumnPositions = "abcdefgh";

        /// <summary>
        /// 行の位置を示す文字列
        /// </summary>
        private const string RowPositions = "12345678";

        /// <summary>
        /// 0～63のインデックスをもとに列インデックスを取得します。
        /// </summary>
        /// <param name="index">マス目のインデックス</param>
        /// <returns>列インデックス</returns>
        public static int GetColumnIndex(int index)
        {
            Debug.Assert(index >= 0 && index < Board.AllLength, $"{index}");
            return index % Board.Length;
        }

        /// <summary>
        /// 0～63のインデックスをもとに行インデックスを取得します。
        /// </summary>
        /// <param name="index">マス目のインデックス</param>
        /// <returns>行インデックス</returns>
        public static int GetRowIndex(int index)
        {
            Debug.Assert(index >= 0 && index < Board.AllLength);
            return index / Board.Length;
        }

        /// <summary>
        /// ターン色の石状態を取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ターン色の石状態</returns>
        public static ulong GetTurnDiscs(GameContext context)
        {
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            return context.Turn == Disc.Color.Black ? context.Black : context.White;
        }

        /// <summary>
        /// ターンと反対色の石状態を取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ターンと反対色の石状態</returns>
        public static ulong GetOppositeDiscs(GameContext context)
        {
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            return context.Turn == Disc.Color.Black ? context.White : context.Black;
        }

        /// <summary>
        /// ターン色の石状態を設定します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="discs">ターン色の石状態</param>
        public static void SetTurnDiscs(GameContext context, ulong discs)
        {
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            if (context.Turn == Disc.Color.Black)
            {
                context.Black = discs;
            }
            else
            {
                 context.White = discs;
            }
        }

        /// <summary>
        /// ターンと反対色の石状態を設定します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="discs">ターンと反対色の石状態</param>
        public static void SetOppositeDiscs(GameContext context, ulong discs)
        {
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            if (context.Turn == Disc.Color.Black)
            {
                context.White = discs;
            }
            else
            {
                context.Black = discs;
            }
        }

        /// <summary>
        /// 指定したインデックスに石が存在するかどうか。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="index">マス目のインデックス</param>
        /// <returns>指定したインデックスに石が存在するかどうか</returns>
        public static bool ExistsDisc(GameContext context, int index)
        {
            return ((context.Black | context.White) & (1ul << index)) > 0;
        }

        /// <summary>
        /// 指定したインデックスに自石が存在するかどうか。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="index">マス目のインデックス</param>
        /// <returns>指定したインデックスに自石が存在するかどうか</returns>
        public static bool ExistsTurnDisc(GameContext context, int index)
        {
            return (GetTurnDiscs(context) & (1ul << index)) > 0;
        }

        /// <summary>
        /// 指定したインデックスに他石が存在するかどうか。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="index">マス目のインデックス</param>
        /// <returns>指定したインデックスに他石が存在するかどうか</returns>
        public static bool ExistsOppsositeDisc(GameContext context, int index)
        {
            return (GetOppositeDiscs(context) & (1ul << index)) > 0;
        }

        /// <summary>
        /// 位置を示す文字列をインデックスに変換します。
        /// </summary>
        /// <param name="position">位置を示す文字列（【例】d2）</param>
        /// <returns>位置を示すインデックス</returns>
        public static int ToIndex(string position)
        {
            Debug.Assert(position != null);
            Debug.Assert(position.Length == 2);

            position = position.Trim();
            var col = position.Substring(0, 1);
            col = Regex.Replace(col, "[ａ-ｚ]", p => ((char)(p.Value[0] - 'ａ' + 'a')).ToString());
            col = Regex.Replace(col, "[Ａ-Ｚ]", p => ((char)(p.Value[0] - 'Ａ' + 'A')).ToString());
            col = Regex.Replace(col, "[０-９]", p => ((char)(p.Value[0] - '０' + '0')).ToString());
            col = col.ToLower();

            var row = position.Substring(1);
            row = Regex.Replace(row, "[ａ-ｚ]", p => ((char)(p.Value[0] - 'ａ' + 'a')).ToString());
            row = Regex.Replace(row, "[Ａ-Ｚ]", p => ((char)(p.Value[0] - 'Ａ' + 'A')).ToString());
            row = Regex.Replace(row, "[０-９]", p => ((char)(p.Value[0] - '０' + '0')).ToString());
            row = row.ToLower();

            if (ColumnPositions.IndexOf(row) != -1 && RowPositions.IndexOf(col) != -1)
            {
                // 逆なので入れ替えてあげる
                var tmp = row;
                row = col;
                col = tmp;
            }

            return (RowPositions.IndexOf(row) * Board.Length) + ColumnPositions.IndexOf(col);
        }

        /// <summary>
        /// 位置を示すインデックスを文字列に変換します。
        /// </summary>
        /// <param name="index">位置を示すインデックス</param>
        /// <returns>位置を示す文字列（【例】d2）</returns>
        public static string ToPosition(int index)
        {
            Debug.Assert(index >= 0 && index < Board.AllLength);
            var col = index % Board.Length;
            var row = index / Board.Length;
            return ColumnPositions.Substring(col, 1) + RowPositions.Substring(row, 1);
        }
    }
}
