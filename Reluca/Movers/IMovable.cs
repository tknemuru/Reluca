using Reluca.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Movers
{
    /// <summary>
    /// 指し手の決定機能を提供します。
    /// </summary>
    public interface IMovable
    {
        /// <summary>
        /// 指し手を決めます。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>指し手</returns>
        int Move(GameContext context);
    }
}
