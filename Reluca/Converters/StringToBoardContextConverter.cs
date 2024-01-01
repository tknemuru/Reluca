using Microsoft.Extensions.DependencyInjection;
using Reluca.Di;
using Reluca.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 文字列を盤コンテキストに変換する機能を提供します。
    /// </summary>
    public class StringToBoardContextConverter : IConvertible<IEnumerable<string>, BoardContext>
    {
        /// <summary>
        /// 文字列を盤コンテキストに変換します。
        /// </summary>
        /// <param name="input">盤コンテキストの文字列</param>
        /// <returns>盤コンテキスト</returns>
        public BoardContext Convert(IEnumerable<string> input)
        {
            // 余分な情報をそぎ落として文字列内の順番を逆転する
            var lines = input
                .Where((line, index) => 0 < index && index <= Board.Length)
                .Select(line => line.Substring(1, Board.Length));
            var joinState = string.Join(string.Empty, lines);

            var context = DiProvider.Get().GetService<BoardContext>();
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
            context.Black = StateToUlong(joinState, Board.Icon.Black);
            context.White = StateToUlong(joinState, Board.Icon.White);
#pragma warning restore CS8602 // null 参照の可能性があるものの逆参照です。
            return context;
        }

        /// <summary>
        /// 盤の状態文字列をulonに変換します。
        /// </summary>
        /// <param name="state">盤の状態文字列</param>
        /// <param name="icon">対象のアイコン文字列</param>
        /// <returns></returns>
        private static ulong StateToUlong(string state, char icon)
        {
            ulong result = 0ul;
            for (var i = 0; i < state.Length; i++)
            {
                if (state[i] == icon)
                {
                    result |= 1ul << i;
                }
            }
            return result;
        }
    }
}
