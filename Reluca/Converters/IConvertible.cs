using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 変換機能を提供します。
    /// </summary>
    public interface IConvertible<TIn, TOut>
    {
        /// <summary>
        /// 変換を実行します。
        /// </summary>
        /// <param name="input">入力情報</param>
        /// <returns>変換した情報</returns>
        TOut Convert(TIn input);
    }
}
