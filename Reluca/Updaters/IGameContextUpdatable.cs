using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reluca.Contexts;

namespace Reluca.Updaters
{
    /// <summary>
    /// ゲーム状態の更新機能を提供します。
    /// </summary>
    public interface IGameContextUpdatable
    {
        void Update(GameContext context);
    }
}
