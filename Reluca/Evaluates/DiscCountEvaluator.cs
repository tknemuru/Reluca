using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 石数による評価機能を提供します。
    /// </summary>
    public class DiscCountEvaluator : IEvaluable
    {
        /// <summary>
        /// パターンインデックスの差分更新を必要とするかどうかを示します。
        /// 石数差のみで評価するため、パターンインデックスを参照せず、常に false を返します。
        /// </summary>
        public bool RequiresPatternIndex => false;

        /// <summary>
        /// ゲーム状態の評価を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ゲーム状態の評価値</returns>
        public long Evaluate(GameContext context)
        {
            return BoardAccessor.GetDiscCount(context.Board, Disc.Color.Black);
        }
    }
}
