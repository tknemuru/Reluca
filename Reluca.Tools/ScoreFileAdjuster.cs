using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools
{
    /// <summary>
    /// スコアファイルの補完機能を提供します。
    /// </summary>
    public class ScoreFileAdjuster
    {
        /// <summary>
        /// 下駄をはかせる数
        /// </summary>
        private static readonly double CeilingDigit = Math.Pow(10, 15);

        /// <summary>
        /// 入力ファイルパス
        /// </summary>
        private const string InputFilePath = "../../../Resources/ScoreFileAdjuster/score.{0}.txt";

        /// <summary>
        /// 出力ファイルパス
        /// </summary>
        private const string OutputFilePath = "../../../Output/ScoreFileAdjuster/evaluated-value.{0}.txt";

        /// <summary>
        /// スコアファイルをRelucaで扱い易い形式に変換します
        /// </summary>
        public static void Adjust()
        {
            for (var stage = 1; stage <= 15; stage++)
            {
                Console.WriteLine($"stage:{stage} start");
                var filePath = string.Format(InputFilePath, stage);
                var csv = string.Join(string.Empty, FileHelper.ReadTextLines(filePath));
                var keyValues = csv.Split(',');
                var length = keyValues.Length;
                Debug.Assert((length % 2 == 0), "要素数が奇数です。");

                for (int i = 0; i < length; i += 2)
                {
                    var key = AdjustKey(keyValues[i]);
                    var value = Convert.ToInt64(double.Parse(keyValues[i + 1]) * CeilingDigit);
                    if (i == 0)
                    {
                        FileHelper.Write($"{key},{value}", string.Format(OutputFilePath, stage));
                    } else
                    {
                        FileHelper.Write($",{key},{value}", string.Format(OutputFilePath, stage));
                    }
                }
                Console.WriteLine($"stage:{stage} end");
            }
        }

        /// <summary>
        /// キーを整形します。
        /// </summary>
        /// <param name="input">キー</param>
        /// <returns>整形したキー文字列</returns>
        private static string AdjustKey(string input)
        {
            var result = AdjustKey(input, FeaturePattern.TypeName.Diag4);
            result = AdjustKey(result, FeaturePattern.TypeName.Diag5);
            result = AdjustKey(result, FeaturePattern.TypeName.Diag6);
            result = AdjustKey(result, FeaturePattern.TypeName.Diag7);
            result = AdjustKey(result, FeaturePattern.TypeName.Diag8);
            result = AdjustKey(result, FeaturePattern.TypeName.HorVert2);
            result = AdjustKey(result, FeaturePattern.TypeName.HorVert3);
            result = AdjustKey(result, FeaturePattern.TypeName.HorVert4);
            result = AdjustKey(result, FeaturePattern.TypeName.Edge2X);
            result = AdjustKey(result, FeaturePattern.TypeName.Corner2X5);
            result = AdjustKey(result, FeaturePattern.TypeName.Corner3X3);
            result = AdjustKey(result, FeaturePattern.TypeName.Parity);
            result = AdjustKey(result, FeaturePattern.TypeName.Mobility);
            return result;
        }

        /// <summary>
        /// キーを整形します。
        /// </summary>
        /// <param name="input">キー</param>
        /// <param name="typeName">特徴パターンの種類名</param>
        /// <returns>整形したキー文字列</returns>
        private static string AdjustKey(string input, string typeName)
        {
            return input.Replace(typeName, $"${typeName}");
        }
    }
}
