using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Converters
{
    /// <summary>
    /// 更新機能を提供します。
    /// </summary>
    public interface IUpdatable<in TIn, out TOut>
    {
        /// <summary>
        /// 更新を実行します。
        /// </summary>
        /// <param name="input">入力情報</param>
        /// <returns>更新に伴い呼び出し元に返却したい情報</returns>
        TOut Update(TIn input);
    }
}
