using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Serchers
{
    /// <summary>
    /// NegaMax法の探索ロジック機能を提供します。
    /// </summary>
    public abstract class NegaMaxTemplate : ISerchable
    {
        /// <summary>
        /// <para>初期アルファ値</para>
        /// </summary>
        protected const long DefaultAlpha = -1000000000000000001L;

        /// <summary>
        /// <para>初期ベータ値</para>
        /// </summary>
        protected const long DefaultBeta = 1000000000000000001L;

        /// <summary>
        /// 評価値
        /// </summary>
        public long Value { get; private set; }

        /// <summary>
        /// 返却するキー
        /// </summary>
        protected int Key { get; set; }

        /// <summary>
        /// <para>コンストラクタ</para>
        /// </summary>
        public NegaMaxTemplate()
        {
            Clear();
        }

        /// <summary>
        /// 記録していた状態をクリアします。
        /// </summary>
        public void Clear()
        {
            Key = GetDefaultKey();
            Value = DefaultAlpha;
        }

        /// <summary>
        /// 探索し、結果を返却します。
        /// </summary>
        /// <param name="context">フィールド状態</param>
        /// <returns>移動方向</returns>
        public virtual int Search(GameContext context)
        {
            Value = SearchBestValue(context, 1, DefaultAlpha, DefaultBeta);
            return Key;
        }

        /// <summary>
        /// <para>最善手を探索して取得する</para>
        /// </summary>
        /// <returns></returns>
        protected long SearchBestValue(GameContext context, int depth, long alpha, long beta)
        {
            // 深さ制限に達した
            if (IsLimit(depth, context)) { return GetEvaluate(context); }

            // 可能な手をすべて生成
            var leafList = GetAllLeaf(context);

            long maxKeyValue = DefaultAlpha;
            if (leafList.Any())
            {
                // ソート
                if (IsOrdering(depth)) { leafList = MoveOrdering(leafList, context); }

                foreach (var leaf in leafList)
                {
                    // 前処理
                    var copyContext = SearchSetUp(context, leaf);

                    long value = SearchBestValue(copyContext, depth + 1, -beta, -alpha) * -1L;

                    // 後処理
                    SearchTearDown(copyContext);

                    // ベータ刈り
                    if (value >= beta)
                    {
                        SetKey(leaf, depth);
                        return value;
                    }

                    if (value > maxKeyValue)
                    {
                        // より良い手が見つかった
                        SetKey(leaf, depth);
                        maxKeyValue = value;
                        // α値の更新
                        alpha = Math.Max(alpha, maxKeyValue);
                    }
                }
            }
            else
            {
                // ▼パスの場合▼
                // 前処理
                var copyContext = PassSetUp(context);

                maxKeyValue = SearchBestValue(copyContext, depth + 1, -beta, -alpha) * -1L;

                // 後処理
                PassTearDown(copyContext);
            }

            Debug.Assert(((maxKeyValue != DefaultAlpha) && (maxKeyValue != DefaultBeta)), "デフォルト値のまま返そうとしています。");
            return maxKeyValue;
        }

        /// <summary>
        /// 返却するキーをセットする
        /// </summary>
        /// <param name="leaf">リーフ</param>
        /// <param name="depth">深さ</param>
        private void SetKey(int leaf, int depth)
        {
            if (depth == 1)
            {
                Key = leaf;
            }
        }

        /// <summary>
        /// 深さ制限に達した場合にはTrueを返す
        /// </summary>
        /// <param name="limit"></param>
        /// <param name="context">ゲーム状態</param>
        /// <returns>深さ制限に達したかどうか</returns>
        protected abstract bool IsLimit(int limit, GameContext context);

        /// <summary>
        /// 評価値を取得する
        /// </summary>
        /// <returns></returns>
        protected abstract long GetEvaluate(GameContext context);

        /// <summary>
        /// 全てのリーフを取得する
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<int> GetAllLeaf(GameContext context);

        /// <summary>
        /// ソートをする場合はTrueを返す
        /// </summary>
        /// <returns></returns>
        protected abstract bool IsOrdering(int depth);

        /// <summary>
        /// ソートする
        /// </summary>
        /// <param name="allLeaf"></param>
        /// <returns></returns>
        protected abstract IEnumerable<int> MoveOrdering(IEnumerable<int> allLeaf, GameContext context);

        /// <summary>
        /// キーの初期値を取得する
        /// </summary>
        /// <returns></returns>
        protected abstract int GetDefaultKey();

        /// <summary>
        /// 探索の前処理を行う
        /// </summary>
        protected abstract GameContext SearchSetUp(GameContext context, int leaf);

        /// <summary>
        /// 探索の後処理を行う
        /// </summary>
        protected abstract GameContext SearchTearDown(GameContext context);

        /// <summary>
        /// パスの前処理を行う
        /// </summary>
        protected abstract GameContext PassSetUp(GameContext context);

        /// <summary>
        /// パスの後処理を行う
        /// </summary>
        protected abstract GameContext PassTearDown(GameContext context);
    }
}
