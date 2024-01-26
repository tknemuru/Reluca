using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Updaters
{
    /// <summary>
    /// ゲーム状態の初期化機能を提供します。
    /// </summary>
    public class InitializeUpdater : IUpdatable<GameContext, GameContext>
    {
        /// <summary>
        /// ゲーム状態を初期状態に更新します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns></returns>
        public GameContext Update(GameContext context)
        {
            context.TurnCount = 0;
            context.Stage = 1;
            context.Turn = Disc.Color.Black;
            context.Black = 34628173824;
            context.White = 68853694464;
            return context;
        }
    }
}
