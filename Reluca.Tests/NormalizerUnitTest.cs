using Reluca.Helpers;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tests
{
    /// <summary>
    /// 正規化機能の単体テスト機能を提供します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class NormalizerUnitTest<T> : BaseUnitTest<T> where T : class
    {
        /// <summary>
        /// 盤状態と3進数の変換辞書
        /// </summary>
        private static readonly Dictionary<char, uint> StateDic = new Dictionary<char, uint>()
        {
            ['白'] = FeaturePattern.BoardStateSequence.White,
            ['空'] = FeaturePattern.BoardStateSequence.Empty,
            ['黒'] = FeaturePattern.BoardStateSequence.Black
        };

        /// <summary>
        /// 文字列の盤状態を3進数の文字列に変換します。
        /// </summary>
        /// <param name="state">文字列の盤状態</param>
        /// <returns>3進数の文字列</returns>
        protected int Convert(string state)
        {
            return RadixHelper.ToInt32(string.Join(string.Empty, state.Select(s => StateDic[s].ToString())), 3);
        }
    }
}
