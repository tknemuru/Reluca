using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Cachers
{
    /// <summary>
    /// キャッシュ機能を提供します。
    /// </summary>
    public interface ICacheable<TKey, TValue>
    {
        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        void Add(TKey key, TValue value);

        /// <summary>
        /// キャッシュを取得します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>値</returns>
        TValue Get(TKey key);

        /// <summary>
        /// キャッシュを取得するとともに、取得できたかどうかの結果を返却します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        /// <returns>キャッシュが取得できたかどうか</returns>
        bool TryGet(TKey key, out TValue value);

        /// <summary>
        /// キャッシュを消去します。
        /// </summary>
        /// <param name="key">キー</param>
        void Dispose(TKey key);
    }
}
