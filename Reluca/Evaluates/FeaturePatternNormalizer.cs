﻿using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Evaluates
{
    /// <summary>
    /// 特徴パターンの正規化機能を提供します。
    /// </summary>
    public class FeaturePatternNormalizer : ResourceReadNormalizer
    {
        /// <summary>
        /// リソースを取得します。
        /// </summary>
        /// <returns>リソース文字列</returns>
        protected override string GetResource()
        {
            return Properties.Resources.feature_pattern_normalize;
        }
    }
}
