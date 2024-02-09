using Reluca.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 特徴パターンによるゲーム状態の評価機能を提供します。
    /// </summary>
    public class FeaturePatternEvaluator : IEvaluable
    {
        /// <summary>
        /// 初期化を行います。
        /// </summary>
        public void Initialize()
        {

        }

        /// <summary>
        /// ゲーム状態の評価を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ゲーム状態の評価値</returns>
        public long Evaluate(GameContext context)
        {
            return 0L;
        }
    }
}
