using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 特徴パターンの正規化機能を提供します。
    /// </summary>
    public interface INormalizable
    {
        /// <summary>
        /// 特徴パターンの正規化を行います。
        /// </summary>
        /// <param name="type">特徴パターン種別</param>
        /// <param name="org">元の値</param>
        /// <returns>正規化した値</returns>
        uint Normalize(FeaturePattern.Type type, uint org);
    }
}
