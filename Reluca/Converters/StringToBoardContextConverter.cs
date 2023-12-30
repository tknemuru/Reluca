using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 文字列を盤コンテキストに変換する機能を提供します。
    /// </summary>
    public class StringToBoardContextConverter : IConvertible<string, BoardContext>
    {
        /// <summary>
        /// 文字列を盤コンテキストに変換します。
        /// </summary>
        /// <param name="input">盤コンテキストの文字列</param>
        /// <returns>盤コンテキスト</returns>
        public BoardContext Convert(string input)
        {
            throw new NotImplementedException();
        }
    }
}
