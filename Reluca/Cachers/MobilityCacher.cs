using Reluca.Contexts;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Cachers
{
#pragma warning disable CS8601
#pragma warning disable CS8602

    /// <summary>
    /// 着手可能情報のキャッシュ機能を提供します。
    /// </summary>
    public class MobilityCacher : ICacheable<GameContext, IEnumerable<int>>
    {
        /// <summary>
        /// キャッシュ
        /// </summary>
        private Dictionary<int, Dictionary<string, IEnumerable<int>>?> Cache {  get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MobilityCacher()
        {
            Cache = new Dictionary<int, Dictionary<string, IEnumerable<int>>?>();
            for (var i = 1; i <= Stage.Max; i++)
            {
                Cache[i] = new Dictionary<string, IEnumerable<int>>();
            }
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void Add(GameContext context, IEnumerable<int> value)
        {
            Cache[context.Stage][GenerateKey(context)] = value;
        }

        /// <summary>
        /// キャッシュを消去します。
        /// </summary>
        /// <param name="key">キー</param>
        public void Dispose(GameContext context)
        {
            for (var i = 0; i < context.Stage; i++)
            {
                Cache[i] = null;
            }
        }

        /// <summary>
        /// キャッシュを取得します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>値</returns>
        public IEnumerable<int> Get(GameContext context)
        {
            return Cache[context.Stage][GenerateKey(context)];
        }

        /// <summary>
        /// キャッシュを取得するとともに、取得できたかどうかの結果を返却します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        /// <returns>キャッシュが取得できたかどうか</returns>
        public bool TryGet(GameContext context, out IEnumerable<int> value)
        {
            return Cache[context.Stage].TryGetValue(GenerateKey(context), out value);
        }

        /// <summary>
        /// キーを生成します。
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static string GenerateKey(GameContext context)
        {
            return $"{context.Turn}|{context.Black}|{context.White}";
        }
    }
}
