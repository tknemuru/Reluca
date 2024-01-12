using Microsoft.Extensions.DependencyInjection;
using Reluca.Di;
using Reluca.Models;

namespace Reluca.Converters
{
    /// <summary>
    /// 着手可能状態を含めた盤状態文字列の変換機能を提供します。
    /// </summary>
    public class StringToMobilityBoardConverter : IConvertible<IEnumerable<string>, GameContext>
    {
        /// <summary>
        /// 文字列を着手可能状態を含めた盤状態に変換します。
        /// </summary>
        /// <param name="input">着手可能状態を含めた盤状態の文字列</param>
        /// <returns>着手可能状態を含めた盤状態</returns>
        public GameContext Convert(IEnumerable<string> input)
        {
            // 余分な情報をそぎ落として文字列内の順番を逆転する
            var lines = input
                .Where((line, index) => 0 < index && index <= Board.Length)
                .Select(line => line.Substring(1, Board.Length));
            var joinState = string.Join(string.Empty, lines);

            var context = new GameContext
            {
                Black = StateToUlong(joinState, Board.Icon.Black),
                White = StateToUlong(joinState, Board.Icon.White),
                Mobility = StateToUlong(joinState, Board.Icon.Mobility)
            };
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
