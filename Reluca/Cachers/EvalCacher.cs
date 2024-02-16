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
    /// 評価値のキャッシュ機能を提供します。
    /// </summary>
    public class EvalCacher : ICacheable<GameContext, long>
    {
        /// <summary>
        /// キャッシュ
        /// </summary>
        private Dictionary<int, Dictionary<string, long>?> Cache {  get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public EvalCacher()
        {
            Cache = new Dictionary<int, Dictionary<string, long>?>();
            for (var i = 1; i <= Stage.Max; i++)
            {
                Cache[i] = new Dictionary<string, long>();
            }
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void Add(GameContext context, long value)
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
        public long Get(GameContext context)
        {
            return Cache[context.Stage][GenerateKey(context)];
        }

        /// <summary>
        /// キャッシュを取得するとともに、取得できたかどうかの結果を返却します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        /// <returns>キャッシュが取得できたかどうか</returns>
        public bool TryGet(GameContext context, out long value)
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
