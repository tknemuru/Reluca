using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Evaluates;
using Reluca.Helpers;
using Reluca.Models;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Serchers
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// NegaMax法の探索機能を提供します。
    /// </summary>
    public class NegaMax : NegaMaxTemplate
    {
        /// <summary>
        /// 深さの制限
        /// </summary>
        private const int DefaultLimitDepth = 9;

        /// <summary>
        /// 評価機能
        /// </summary>
        private IEvaluable? Evaluator {  get; set; }

        /// <summary>
        /// 着手可能数分析機能
        /// </summary>
        private MobilityAnalyzer? MobilityAnalyzer { get; set; }

        /// <summary>
        /// 指し手による石の裏返し更新機能
        /// </summary>
        private MoveAndReverseUpdater? ReverseUpdater { get; set; }

        /// <summary>
        /// 探索する深さ
        /// </summary>
        protected int LimitDepth { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public NegaMax()
            : base()
        {
            Evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            MobilityAnalyzer = DiProvider.Get().GetService<MobilityAnalyzer>();
            ReverseUpdater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
            LimitDepth = DefaultLimitDepth;
        }

        /// <summary>
        /// 初期化を行います。
        /// </summary>
        /// <param name="evaluator">評価機能</param>
        /// <param name="limitDepth">探索の深さ</param>
        public void Initialize(IEvaluable evaluator, int limitDepth)
        {
            Evaluator = evaluator;
            LimitDepth = limitDepth;
        }

        /// <summary>
        /// 深さ制限に達した場合にはTrueを返す
        /// </summary>
        /// <param name="depth">深さ</param>
        /// <param name="context">ゲーム状態</param>
        /// <returns>深さ制限に達したかどうか</returns>
        protected override bool IsLimit(int depth, GameContext context)
        {
            return depth >= DefaultLimitDepth || BoardAccessor.IsGameEndTurnCount(context);
        }

        /// <summary>
        /// 評価値を取得する
        /// </summary>
        /// <returns>評価値</returns>
        protected override long GetEvaluate(GameContext context)
        {
            var score = Evaluator.Evaluate(context);
            return score * GetParity(context);
        }

        /// <summary>
        /// 全てのリーフを取得する
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<int> GetAllLeaf(GameContext context)
        {
            return MobilityAnalyzer.Analyze(context);
        }

        /// <summary>
        /// ソートをする場合はTrueを返す
        /// </summary>
        /// <returns></returns>
        protected override bool IsOrdering(int depth)
        {
            return depth <= 3;
        }

        /// <summary>
        /// ソートする
        /// </summary>
        /// <param name="allLeaf"></param>
        /// <returns></returns>
        protected override IEnumerable<int> MoveOrdering(IEnumerable<int> allLeaf, GameContext context)
        {
            var orderd = allLeaf
                .OrderByDescending(move =>
                {
                    var copyContext = SearchSetUp(context, move);
                    BoardAccessor.Pass(copyContext);
                    return GetEvaluate(copyContext);
                });
            return orderd;
        }

        /// <summary>
        /// キーの初期値を取得する
        /// </summary>
        /// <returns></returns>
        protected override int GetDefaultKey()
        {
            return -1;
        }

        /// <summary>
        /// 探索の前処理を行う
        /// </summary>
        protected override GameContext SearchSetUp(GameContext context, int move)
        {
            var copyContext = BoardAccessor.DeepCopy(context);

            // 指す
            copyContext.Move = move;
            ReverseUpdater.Update(copyContext);

            // ターンをまわす
            BoardAccessor.NextTurn(copyContext);

            return copyContext;
        }

        /// <summary>
        /// 探索の後処理を行う
        /// </summary>
        protected override GameContext SearchTearDown(GameContext context)
        {
            return context;
        }

        /// <summary>
        /// パスの前処理を行う
        /// </summary>
        protected override GameContext PassSetUp(GameContext context)
        {
            // パスする
            BoardAccessor.Pass(context);
            return context;
        }

        /// <summary>
        /// パスの後処理を行う
        /// </summary>
        protected override GameContext PassTearDown(GameContext context)
        {
            return context;
        }

        /// <summary>
        /// パリティ値を取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>パリティ値</returns>
        private static long GetParity(GameContext context)
        {
            return context.Turn == Disc.Color.Black ? 1L : -1L;
        }
    }
}
