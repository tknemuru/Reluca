using Reluca.Contexts;
using Reluca.Models;
using Reluca.Updaters;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reluca.Analyzers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 着手可能情報の分析機能を提供します。
    /// </summary>
    public class MobilityAnalyzer
    {
        /// <summary>
        /// 石の裏返し更新機能
        /// </summary>
        private readonly MoveAndReverseUpdater _updater;

        /// <summary>
        /// コンストラクタ。DI からの依存注入を受け付けます。
        /// </summary>
        /// <param name="updater">石の裏返し更新機能</param>
        public MobilityAnalyzer(MoveAndReverseUpdater updater)
        {
            _updater = updater;
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
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能位置のリスト</returns>
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
                // 配置可能状態をリセットしておく
                context.Mobility = 0ul;
                for (var i = 0; i < Board.AllLength; i++)
                {
                    var valid = _updater.Update(context, i);
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

        /// <summary>
        /// 着手可能数のみをカウントして返します。
        /// リストのアロケーションを行わないため、カウントのみが必要な場合はこちらを使用してください。
        /// MoveAndReverseUpdater.Update は analyze モード（第2引数 >= 0）で呼び出されるため、
        /// 盤面への副作用はありません。context.Turn のみ一時的に変更しますが、finally で復元します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="turn">分析対象のターン</param>
        /// <returns>着手可能数</returns>
        public int AnalyzeCount(GameContext context, Disc.Color turn)
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
                context.Turn = turn;
                context.Mobility = 0ul;
                int count = 0;
                for (var i = 0; i < Board.AllLength; i++)
                {
                    if (_updater.Update(context, i))
                    {
                        count++;
                    }
                }
                return count;
            }
            finally
            {
                context.Turn = orgTurn;
            }
        }
    }
}
