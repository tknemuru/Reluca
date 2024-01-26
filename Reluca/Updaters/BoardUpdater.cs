using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Converters;
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
    public class BoardUpdater : IUpdatable<GameContext, bool>
    {
        /// <summary>
        /// 指し手による盤の更新を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>有効な指し手であるかどうか</returns>
        public bool Update(GameContext context)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Move >= 0);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var validMove = false;
            var i = context.Move;
            var turn = BoardAccessor.GetTurnDiscs(context);
            var opposite = BoardAccessor.GetOppositeDiscs(context);
            // 指し手の場所に既に石が配置済であれば何もせず終了
            if (BoardAccessor.ExistsDisc(context, i))
            {
                return validMove;
            }

            // 反対色の石を裏返していく
            var tmpTurn = turn;
            var tmpOppsite = opposite;
            var valid = false;
            var hasReversed = false;
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
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 左
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i - 1;
            currentLine = startLine;
            while (currentLine == startLine)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i + Board.Length;
            while (index < Board.AllLength)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 右上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i + 1 - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 左下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i - 1 + Board.Length;
            var orgColIndex = -1;
            if (index < Board.AllLength)
            {
                orgColIndex = BoardAccessor.GetColumnIndex(index);
            }
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) <= orgColIndex)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 左上
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i - 1 - Board.Length;
            while (index >= 0)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            // 右下
            tmpTurn = turn;
            tmpOppsite = opposite;
            valid = false;
            hasReversed = false;
            index = i + 1 + Board.Length;
            orgColIndex = 99;
            if (index < Board.AllLength)
            {
                orgColIndex = BoardAccessor.GetColumnIndex(index);
            }
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) >= orgColIndex)
            {
                // 空マスが存在したら不成立
                if (!BoardAccessor.ExistsDisc(context, index))
                {
                    break;
                }
                // 一つ以上反対の色が存在して裏返したか
                if (BoardAccessor.ExistsOppsositeDisc(context, index))
                {
                    hasReversed = true;
                }
                // 自石が存在して一つ以上裏返し済であれば成立して終了
                if (BoardAccessor.ExistsTurnDisc(context, index) && hasReversed)
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
                validMove = true;
            }

            if (validMove)
            {
                // 有効な指し手であれば、最後に指し手自体を配置する
                turn |= 1ul << context.Move;
            }

            // 結果をコンテキストに反映
            BoardAccessor.SetTurnDiscs(context, turn);
            BoardAccessor.SetOppositeDiscs(context, opposite);

            return validMove;
        }
    }
}
