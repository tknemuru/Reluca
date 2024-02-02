using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Movers
{
    /// <summary>
    /// 初めに見つけた有効な指し手の返却機能を提供します。
    /// </summary>
    public class FindFirstMover : IMovable
    {
        /// <summary>
        /// 指し手を決めます。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>指し手</returns>
        public int Move(GameContext context)
        {
            var result = -1;
            var mobility = context.Mobility;
            for (var i = 0; i < Board.AllLength; i++)
            {
                if ((mobility & (1ul << i)) > 0)
                {
                    result = i;
                    break;
                }
            }
            return result;
        }
    }
}
