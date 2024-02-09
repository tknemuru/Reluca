using Reluca.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// ゲーム状態の評価機能を提供します。
    /// </summary>
    public interface IEvaluable
    {
        /// <summary>
        /// ゲーム状態を評価します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ゲーム状態の評価値</returns>
        long Evaluate(GameContext context);
    }
}
