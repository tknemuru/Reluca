using Reluca.Contexts;
using Reluca.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reluca.Analyzers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 着手可能情報の分析機能を提供します。
    /// ビットボード演算による高速合法手生成を使用します。
    /// </summary>
    public class MobilityAnalyzer
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public MobilityAnalyzer()
        {
        }

        /// <summary>
        /// 着手可能情報を分析して取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>着手可能位置のリスト</returns>
        public List<int> Analyze(GameContext context)
        {
            return Analyze(context, Disc.Color.Undefined);
        }

        /// <summary>
        /// 着手可能情報を分析して取得します。
        /// ビットボード演算により、全合法手を一括で算出します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能位置のリスト</returns>
        public List<int> Analyze(GameContext context, Disc.Color turn)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var (player, opponent) = GetPlayerOpponent(context, turn);
            ulong moves = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            // context.Mobility はここでは更新しない（MobilityUpdater の責務）
            // 探索エンジンでの使用時は PvsSearchEngine が管理する
            return BitboardMobilityGenerator.ToMoveList(moves);
        }

        /// <summary>
        /// 着手可能数のみをカウントして返します。
        /// ビットボード演算と PopCount により、リストのアロケーションなしにカウントを返します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能数</returns>
        public int AnalyzeCount(GameContext context, Disc.Color turn)
        {
            Debug.Assert(context != null);
            Debug.Assert(context.Turn != Disc.Color.Undefined);

            var (player, opponent) = GetPlayerOpponent(context, turn);
            ulong moves = BitboardMobilityGenerator.GenerateMoves(player, opponent);
            return BitboardMobilityGenerator.CountMoves(moves);
        }

        /// <summary>
        /// 手番に基づいて player と opponent のビットボードを取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">手番の色。Undefined の場合は context.Turn を使用します。</param>
        /// <returns>player と opponent のビットボードのタプル</returns>
        private static (ulong player, ulong opponent) GetPlayerOpponent(GameContext context, Disc.Color turn)
        {
            if (turn == Disc.Color.Undefined) turn = context.Turn;
            return turn == Disc.Color.Black
                ? (context.Black, context.White)
                : (context.White, context.Black);
        }
    }
}
