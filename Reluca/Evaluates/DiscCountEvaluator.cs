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
