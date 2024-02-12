using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Analyzers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 着手可能情報の分析機能を提供します。
    /// </summary>
    public class MobilityAnalyzer
    {
        /// <summary>
        /// 着手可能情報を分析して取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能情報</returns>
        public List<int> Analyze(GameContext context)
        {
            return Analyze(context, Disc.Color.Undefined);
        }

        /// <summary>
        /// 着手可能情報を分析して取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能情報</returns>
        public List<int> Analyze(GameContext context, Disc.Color turn)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var orgTurn = context.Turn;
            if (turn == Disc.Color.Undefined)
            {
                turn = context.Turn;
            }

            try
            {
                // ターンを分析対象のターンに変更する
                context.Turn = turn;

                var mobilitys = new List<int>();
                var updater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
                // 配置可能状態をリセットしておく
                context.Mobility = 0ul;
                for (var i = 0; i < Board.AllLength; i++)
                {
                    var valid = updater.Update(context, i);
                    if (valid)
                    {
                        // 有効な指し手を記録
                        mobilitys.Add(i);
                    }
                }
                return mobilitys;
            } finally
            {
                // 必ずターンを元に戻しておく
                context.Turn = orgTurn;
            }
        }
    }
}
