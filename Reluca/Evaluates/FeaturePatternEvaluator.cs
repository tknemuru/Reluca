using Reluca.Accessors;
using Reluca.Analyzers;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 特徴パターンによるゲーム状態の評価機能を提供します。
    /// </summary>
    public class FeaturePatternEvaluator : IEvaluable
    {
        /// <summary>
        /// キーと桁数を分離する文字
        /// </summary>
        private const char KeyDigitSeparator = '$';

        /// <summary>
        /// ステージ・特徴パターンごとの評価値
        /// </summary>
        private static Dictionary<int, Dictionary<FeaturePattern.Type, Dictionary<int, long>>>? EvaluatedValues;

        /// <summary>
        /// 特徴パターン抽出機能
        /// </summary>
        private FeaturePatternExtractor? Extractor {  get; set; }

        /// <summary>
        /// 着手可能数分析機能
        /// </summary>
        private MobilityAnalyzer? MobilityAnalyzer { get; set; }

        /// <summary>
        /// 評価値符号の正規化機能
        /// </summary>
        private EvaluatedValueSignNoramalizer? EvalSignNormalizer {  get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FeaturePatternEvaluator()
        {
            EvaluatedValues = [];
            Extractor = DiProvider.Get().GetService<FeaturePatternExtractor>();
            MobilityAnalyzer = DiProvider.Get().GetService<MobilityAnalyzer>();
            EvalSignNormalizer = DiProvider.Get().GetService<EvaluatedValueSignNoramalizer>();
        }

        /// <summary>
        /// 初期化を行います。
        /// </summary>
        public void Initialize(FeaturePatternExtractor extractor, MobilityAnalyzer mobility, EvaluatedValueSignNoramalizer evalSignNormalizer)
        {
            Extractor = extractor;
            MobilityAnalyzer = mobility;
            EvalSignNormalizer = evalSignNormalizer;
        }

        /// <summary>
        /// ゲーム状態の評価を行います。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>ゲーム状態の評価値</returns>
        public long Evaluate(GameContext context)
        {
            LoadValues(context.Stage);

            var eval = 0L;
            var parity = GetParity(context);

            // 着手可能数
            var black = MobilityAnalyzer.Analyze(context, Disc.Color.Black).Count;
            var white = MobilityAnalyzer.Analyze(context, Disc.Color.White).Count;
            var mobility = black - white;
            eval += EvaluatedValues[context.Stage][FeaturePattern.Type.Mobility][0] * mobility;

            // ゲーム終了であれば最大値 or 0 or 最小値を返却して処理終了
            if (black <= 0 && white <= 0)
            {
                var blackCount = BoardAccessor.GetDiscCount(context.Board, Disc.Color.Black);
                var whiteCount = BoardAccessor.GetDiscCount(context.Board, Disc.Color.White);
                var resultCount = blackCount - whiteCount;
                if (resultCount > 0)
                {
                    return long.MaxValue;
                }
                if (resultCount < 0)
                {
                    return long.MinValue;
                }
                return 0;
            }

            // パターンによる評価値
            var patterns = Extractor.Extract(context.Board);
            foreach (var pattern in patterns)
            {
                foreach (var index in pattern.Value)
                {
                    if (EvaluatedValues[context.Stage][pattern.Key].TryGetValue(index, out long value))
                    {
                        eval += value * EvalSignNormalizer.Normalize(pattern.Key, index);
                    }
                }
            }

            // パリティ
            eval += EvaluatedValues[context.Stage][FeaturePattern.Type.Parity][0] * parity;

            return eval;
        }

        /// <summary>
        /// 評価値を読み込みます。
        /// </summary>
        /// <param name="stage">読み込み対象のステージ</param>
        private void LoadValues(int stage)
        {
            // 既に読み込み済なら何もしない
            if (EvaluatedValues.ContainsKey(stage))
            {
                return;
            }

            var csv = GetResource(stage);
            var keyValues = csv.Split(',');
            var length = keyValues.Length;
            Debug.Assert((length % 2 == 0), "要素数が奇数です。");

            EvaluatedValues[stage] = new Dictionary<FeaturePattern.Type, Dictionary<int, long>>();
            for (int i = 0; i < length; i += 2)
            {
                var indexAndType = keyValues[i].Split(KeyDigitSeparator);
                Debug.Assert(indexAndType.Length == 2, "要素数が不正です。");
                var index = int.Parse(indexAndType[0]);
                var type = FeaturePattern.GetType(indexAndType[1]);
                var eval = long.Parse(keyValues[i + 1]);
                if (!EvaluatedValues[stage].ContainsKey(type))
                {
                    EvaluatedValues[stage][type] = new Dictionary<int, long>();
                }
                EvaluatedValues[stage][type][index] = eval;
            }
        }

        /// <summary>
        /// リソースを取得します。
        /// </summary>
        /// <param name="stage">ステージ</param>
        /// <returns>リソースファイルの文字列</returns>
        /// <exception cref="ArgumentException">ステージが不正</exception>
        private static string GetResource(int stage)
        {
            string? resource = stage switch
            {
                1 => Properties.Resources.evaluated_value_1,
                2 => Properties.Resources.evaluated_value_2,
                3 => Properties.Resources.evaluated_value_3,
                4 => Properties.Resources.evaluated_value_4,
                5 => Properties.Resources.evaluated_value_5,
                6 => Properties.Resources.evaluated_value_6,
                7 => Properties.Resources.evaluated_value_7,
                8 => Properties.Resources.evaluated_value_8,
                9 => Properties.Resources.evaluated_value_9,
                10 => Properties.Resources.evaluated_value_10,
                11 => Properties.Resources.evaluated_value_11,
                12 => Properties.Resources.evaluated_value_12,
                13 => Properties.Resources.evaluated_value_13,
                14 => Properties.Resources.evaluated_value_14,
                15 => Properties.Resources.evaluated_value_15,
                _ => throw new ArgumentException("ステージが不正です。"),
            };
            return resource;
        }

        /// <summary>
        /// パリティ値を取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>パリティ値</returns>
        private static long GetParity(GameContext context)
        {
            return context.Turn == Disc.Color.Black ? 1 : 0;
        }
    }
}
