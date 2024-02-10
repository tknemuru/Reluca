using Newtonsoft.Json;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Di;
using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Reluca.Tools
{
    /// <summary>
    /// 盤の特徴を示すパターンの作成機能を提供します。
    /// </summary>
    public class FeaturePatternCreator
    {
        /// <summary>
        /// 特徴パターンの作成を実行します。
        /// </summary>
        public static void Execute()
        {
            var input = FileHelper.ReadTextLines("../../../Resources/FeaturePatternCreator/feature-pattern-input.txt");
            var output = Create(input);
            FileHelper.WriteJson(output, "../../../Output/FeaturePatternCreator/feature-pattern.json", Formatting.Indented);
        }

        /// <summary>
        /// 盤の特徴を示すパターンを作成します。
        /// </summary>
        /// <param name="input">ファイルから読み込んだ盤文字列</param>
        /// <returns>盤の特徴を示すパターン</returns>
        public static Dictionary<string, List<List<ulong>>> Create(IEnumerable<string> input)
        {
            var result = new Dictionary<string, List<List<ulong>>>();
            var unit = new List<string>();
            var key = string.Empty;
            foreach (var value in input)
            {
                var val = value.Trim();
                if (val == string.Empty) continue;
                if (val.Contains(SimpleText.ContextSeparator))
                {
                    if (unit.Count > 0)
                    {
                        if (!result.ContainsKey(key))
                        {
                            result[key] = new List<List<ulong>>();
                        }
                        result[key].Add(Convert(unit));
                    }
                    unit.Clear();
                    key = val.Replace("-", string.Empty);
                    continue;
                }
                unit.Add(val);
            }
            if (unit.Count > 0)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = new List<List<ulong>>();
                }
                result[key].Add(Convert(unit));
            }
            return result;
        }

        /// <summary>
        /// 文字列を特徴パターンのマスクリストに変換します。
        /// </summary>
        /// <param name="input">着手可能状態を含めた盤状態の文字列</param>
        /// <returns>着手可能状態を含めた盤状態</returns>
        public static List<ulong> Convert(IEnumerable<string> input)
        {
            // 余分な情報をそぎ落として文字列内の順番を逆転する
            var lines = input
                .Where((line, index) => 0 < index && index <= Board.Length)
                .Select(line => line.Substring(1, Board.Length));
            var state = string.Join(string.Empty, lines);

            var positions = new Dictionary<int, ulong>();
            for (var i = 0; i < state.Length; i++)
            {
                if (state[i] != '　')
                {
                    var seq = int.Parse(Regex.Replace(state[i].ToString(), "[０-９]", p => ((char)(p.Value[0] - '０' + '0')).ToString()));
                    var position = 1ul << i;
                    positions[seq] = position;
                }
            }

            var result = positions
                .OrderBy(p => p.Key)
                .Select(p => p.Value)
                .ToList();
            return result;
        }
    }
}
