using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Models;
using Reluca.Services;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 有効な盤状態の抽出機能を提供します。
    /// </summary>
    public class ValidStateExtractor
    {
        /// <summary>
        /// 出力先ファイルパス
        /// </summary>
        private const string OutputFilePath = "../../../../Output/ValidStateExtractor/state.{0}.txt";

        /// <summary>
        /// 統計情報
        /// </summary>
        private static readonly Dictionary<int, ulong> StatisticsInfo = new Dictionary<int, ulong>();

        /// <summary>
        /// 着手可能数分析機能
        /// </summary>
        private static readonly MobilityAnalyzer? MobilityAnalyzer = DiProvider.Get().GetService<MobilityAnalyzer>();

        /// <summary>
        /// 指し手による石の裏返し更新機能
        /// </summary>
        private static readonly MoveAndReverseUpdater? ReverseUpdater = DiProvider.Get().GetService<MoveAndReverseUpdater>();

        /// <summary>
        /// ゲーム終了判定機能
        /// </summary>
        private static readonly GameEndJudge? GameEndJudge = DiProvider.Get().GetService<GameEndJudge>();

        /// <summary>
        /// 有効な盤状態を抽出します。
        /// </summary>
        public void Extract()
        {
            for (var i = 0; i < 60; i++)
            {
                StatisticsInfo[i] = 0L;
            }

            var context = new GameContext();
            DiProvider.Get().GetService<InitializeUpdater>().Update(context);
            Extract(context);
        }

        /// <summary>
        /// 有効な盤状態を抽出します。
        /// </summary>
        /// <param name="context"></param>
        private void Extract(GameContext context)
        {
            // 終盤は抽出対象外
            if (20 <= context.TurnCount)
            {
                return;
            }

            // 可能な手をすべて生成
            var leafList = GetAllLeaf(context);
            var count = leafList.Count();
            StatisticsInfo[context.TurnCount] += ulong.Parse(count.ToString());
            FileHelper.Log($"---------------");
            FileHelper.Log($"ターン:{context.TurnCount} 今回の数:{count} 累計:{StatisticsInfo[context.TurnCount]}");
            //FileHelper.Log(DiProvider.Get().GetService<GameContextToStringConverter>().Convert(context));
            //var leafs = string.Join(",", leafList.Select(l => BoardAccessor.ToPosition(l)));
            //FileHelper.Log($"leafs:{leafs}");


            if (leafList.Any())
            {
                foreach (var leaf in leafList)
                {
                    // 指す
                    var copyContext = BoardAccessor.DeepCopy(context);
                    copyContext.Move = leaf;
                    ReverseUpdater.Update(copyContext);

                    // 書き込み
                    FileHelper.Write($"{RadixHelper.ToString(copyContext.Black, 32,true)}|{RadixHelper.ToString(copyContext.White, 32, true)},", string.Format(OutputFilePath, copyContext.TurnCount));

                    // ターンをまわす
                    BoardAccessor.NextTurn(copyContext);

                    // 再帰的に展開していく
                    Extract(copyContext);
                }
            }
            else
            {
                // ▼パスの場合▼

                // ゲーム終了ならば探索終了
                if(GameEndJudge.Execute(context))
                {
                    return;
                }

                // 前処理
                var copyContext = BoardAccessor.DeepCopy(context);
                BoardAccessor.Pass(copyContext);

                // 再帰的に展開していく
                Extract(copyContext);
            }
        }

        /// <summary>
        /// 全てのリーフを取得する
        /// </summary>
        /// <param name="context">ゲームコンテキスト</param>
        /// <returns>着手可能なインデックスの列挙</returns>
        private IEnumerable<int> GetAllLeaf(GameContext context)
        {
            return MobilityAnalyzer.Analyze(context);
        }
    }
}
