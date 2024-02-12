using Reluca.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Serchers
{
    /// <summary>
    /// 探索機能を提供します。
    /// </summary>
    public interface ISerchable
    {
        /// <summary>
        /// ゲーム状態を元に探索し、最善の指し手を返却します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>探索結果の指し手</returns>
        int Search(GameContext context);
    }
}
