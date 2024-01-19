using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Updaters
{
    /// <summary>
    /// 指し手による盤の更新機能を提供します。
    /// </summary>
    public class BoardUpdater : IGameContextUpdatable
    {
        /// <summary>
        /// 指し手による
        /// </summary>
        /// <param name="context"></param>
        public void Update(GameContext context)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Move > 0);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var i = context.Move;
            var turn = BoardAccessor.GetTurnDiscs(context);
            var opposite = BoardAccessor.GetOppositeDiscs(context);
            // 指し手の場所に既に石が配置済であれば何もせず終了
            if (BoardAccessor.ExistsDisc(context, i))
            {
                return;
            }

            // 指し手を配置する
            turn |= 1ul << i;

            // 反対色の石を裏返していく
            var tmpTurn = turn;
            var tmpOppsite = opposite;
            var valid = false;
            var index = i + 1;
            int startLine = index / Board.Length;
            int currentLine = startLine;
            // 右
            while (currentLine == startLine)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index++;
                currentLine = index / Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 左
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i - 1;
            currentLine = startLine;
            while (currentLine == startLine)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index--;
                currentLine = index / Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index -= Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i + Board.Length;
            while (index < Board.AllLength)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index += Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 右上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i + 1 - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index++;
                index -= Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 左下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i - 1 + Board.Length;
            var orgColIndex = BoardAccessor.GetColumnIndex(index);
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) <= orgColIndex)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index--;
                index += Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 左上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i - 1 - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index--;
                index -= Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 右下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            index = i + 1 + Board.Length;
            orgColIndex = BoardAccessor.GetColumnIndex(index);
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) >= orgColIndex)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 自石が存在したら成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index))
                {
                    valid = true;
                    break;
                }
                tmpTurn |= 1ul << index;
                tmpOppsite &= ~(1ul << index);
                index++;
                index += Board.Length;
            }
            if (valid)
            {
                turn = tmpTurn;
                opposite = tmpOppsite;
            }

            // 結果をコンテキストに反映
            BoardAccessor.SetTurnDiscs(context, turn);
            BoardAccessor.SetOppositeDiscs(context, opposite);
        }
    }
}
