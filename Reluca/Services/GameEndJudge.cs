using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Services
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// ゲーム終了の判定機能を提供します。
    /// </summary>
    public class GameEndJudge : IServiceable<GameContext, bool>
    {
        /// <summary>
        /// ゲーム終了かどうかを判定します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ゲーム終了かどうか</returns>
        public bool Execute(GameContext context)
        {
            // 盤がすべて埋まっていたらゲーム終了
            if ((context.Black | context.White) == ulong.MaxValue)
            {
                return true;
            }

            // 着手可能な指し手があるか
            var _context = BoardAccessor.DeepCopy(context);
            var updater = DiProvider.Get().GetService<MobilityUpdater>();
            var ownResult = updater.Update(_context);
            // 自身の指し手が存在したらゲーム続行
            if (ownResult.Count > 0)
            {
                return false;
            }
            BoardAccessor.ChangeOppositeTurn(_context);
            var oppositeResult = updater.Update(_context);
            // 相手の指し手が存在したらゲーム続行
            if (oppositeResult.Count > 0)
            {
                return false;
            }

            return true;
        }
    }
}
