using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 盤コンテキストの文字列変換機能を提供します。
    /// </summary>
    public class BoardContextToStringConverter : IConvertible<BoardContext, string>
    {
        /// <summary>
        /// 盤コンテキストを文字列に変換します。
        /// </summary>
        /// <param name="input">盤コンテキスト</param>
        /// <returns>文字列に変化した盤コンテキスト</returns>
        public string Convert(BoardContext input)
        {
            throw new NotImplementedException();
        }
    }
}
