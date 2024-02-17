using Reluca.Accessors;
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
    /// 裏返し結果のキャッシュ機能を提供します。
    /// </summary>
    public class ReverseResultCacher : ICacheable<GameContext, BoardContext>
    {
        /// <summary>
        /// キャッシュ
        /// </summary>
        private Dictionary<int, Dictionary<string, BoardContext>?> Cache { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ReverseResultCacher()
        {
            Cache = new Dictionary<int, Dictionary<string, BoardContext>?>();
            for (var i = 1; i <= Stage.Max; i++)
            {
                Cache[i] = new Dictionary<string, BoardContext>();
            }
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        public void Add(GameContext context, BoardContext value)
        {
            Cache[context.Stage][GenerateKey(context)] = value;
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="move">指し手</param>
        /// <param name="context">コンテクスト</param>
        /// <param name="value">値</param>
        public void Add(int move, GameContext context, BoardContext value)
        {
            Cache[context.Stage][GenerateKey(move, context)] = BoardAccessor.DeepCopy(value);
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
        public BoardContext Get(GameContext context)
        {
            return Cache[context.Stage][GenerateKey(context)];
        }

        /// <summary>
        /// キャッシュを取得するとともに、取得できたかどうかの結果を返却します。
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        /// <returns>キャッシュが取得できたかどうか</returns>
        public bool TryGet(GameContext context, out BoardContext value)
        {
            return Cache[context.Stage].TryGetValue(GenerateKey(context), out value);
        }

        /// <summary>
        /// キーを生成します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>キー</returns>
        private static string GenerateKey(GameContext context)
        {
            return $"{context.Move}|{context.Black}|{context.White}";
        }

        /// <summary>
        /// キーを生成します。
        /// </summary>
        /// <param name="move">指し手</param>
        /// <param name="context">ゲーム状態</param>
        /// <returns>キー</returns>
        private static string GenerateKey(int move, GameContext context)
        {
            return $"{move}|{context.Black}|{context.White}";
        }
    }
}
