using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Updaters
{
    /// <summary>
    /// 配置可能状態の更新機能を提供します。
    /// </summary>
    public class MobilityUpdater : IUpdatable<GameContext, int>
    {
        /// <summary>
        /// 配置可能状態の更新を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>有効な指し手の配置結果</returns>
        public int Update(GameContext context)
        {
            return Update(context, Disc.Color.Undefined);
        }

        /// <summary>
        /// 配置可能状態の更新を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>有効な指し手の配置結果</returns>
        public int Update(GameContext context, Disc.Color turn)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            if (turn == Disc.Color.Undefined)
            {
                turn = context.Turn;
            }

            var mobilityCount = 0;
            var updater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
            // 配置可能状態をリセットしておく
            context.Mobility = 0ul;
            for (var i = 0; i < Board.AllLength; i++)
            {
                //var orgBoard = context.Board with { };
                var targetContext = context with { Move = i, Turn = turn, Board = context.Board with { } };
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
                var valid = updater.Update(targetContext);
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
                if (valid)
                {
                    // 配置可能情報を更新
                    context.Mobility |= 1ul << i;

                    // 有効な指し手の配置結果を記録しておく
                    //BoardAccessor.SetDisc(orgBoard, context.Turn, i);
                    //validResults[orgBoard.GetHashCode()] = targetContext;
                    mobilityCount++;
                }
            }
            return mobilityCount;
        }
    }
}
