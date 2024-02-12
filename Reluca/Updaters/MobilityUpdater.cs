using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Updaters
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 配置可能状態の更新機能を提供します。
    /// </summary>
    public class MobilityUpdater : IUpdatable<GameContext, List<int>>
    {
        /// <summary>
        /// 配置可能状態の更新を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>有効な指し手の配置結果</returns>
        public List<int> Update(GameContext context)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var mobilitys = new List<int>();
            var updater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
            // 配置可能状態をリセットしておく
            context.Mobility = 0ul;
            for (var i = 0; i < Board.AllLength; i++)
            {
                var valid = updater.Update(context, i);
                if (valid)
                {
                    // 配置可能情報を更新
                    context.Mobility |= 1ul << i;

                    // 有効な指し手を記録しておく
                    mobilitys.Add(i);
                }
            }
            return mobilitys;
        }
    }
}
