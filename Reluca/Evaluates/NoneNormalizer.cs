using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 正規化せずそのまま返却する機能を提供します。
    /// </summary>
    public class NoneNormalizer : INormalizable
    {
        /// <summary>
        /// 特徴パターンをそのまま返却します
        /// </summary>
        /// <param name="type">特徴パターンの種別</param>
        /// <param name="org">元の値</param>
        /// <returns>元の値</returns>
        public ushort Normalize(FeaturePattern.Type type, ushort org)
        {
            return org;
        }
    }
}
