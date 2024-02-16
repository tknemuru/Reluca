using Reluca.Cachers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Serchers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Movers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
#pragma warning disable CS8604
    /// <summary>
    /// 最善手の返却機能を提供します。
    /// </summary>
    public class FindBestMover : IMovable
    {
        /// <summary>
        /// 探索機能
        /// </summary>
        private CachedNegaMax? Searcher { get; set; }

        /// <summary>
        /// 指し手を決めます。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>指し手</returns>
        public int Move(GameContext context)
        {
            Searcher = DiProvider.Get().GetService<CachedNegaMax>();
            if (context.TurnCount >= 46)
            {
                Searcher.Initialize(DiProvider.Get().GetService<DiscCountEvaluator>(), 99);
            }
            return Searcher.Search(context);
        }
    }
}
