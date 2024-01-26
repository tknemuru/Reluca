using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Services
{
    /// <summary>
    /// サービス提供可能であることを示します。
    /// </summary>
    /// <typeparam name="TIn">入力情報</typeparam>
    /// <typeparam name="TOut">出力情報</typeparam>
    public interface IServiceable<in TIn, out TOut>
    {
        TOut Execute(TIn input);
    }
}
