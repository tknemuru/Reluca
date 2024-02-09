using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 評価値の正規化機能を提供します。
    /// </summary>
    public class EvaluatedValueSignNoramalizer : ResourceReadNormalizer
    {
        /// <summary>
        /// リソースを取得します。
        /// </summary>
        /// <returns>リソース文字列</returns>
        protected override string GetResource()
        {
            return Properties.Resources.evaluated_value_sign_normalize;
        }
    }
}
