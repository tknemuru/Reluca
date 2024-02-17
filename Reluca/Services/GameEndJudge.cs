using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
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
        /// 着手可能数分析機能
        /// </summary>
        private MobilityAnalyzer? MobilityAnalyzer { get; set; }

        public GameEndJudge()
        {
            MobilityAnalyzer = DiProvider.Get().GetService<MobilityAnalyzer>();
        }

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
            var black = MobilityAnalyzer.Analyze(context, Disc.Color.Black).Count;
            var white = MobilityAnalyzer.Analyze(context, Disc.Color.White).Count;
            return black <= 0 && white <= 0;
        }
    }
}
