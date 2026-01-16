/// <summary>
/// 【ModuleDoc】
/// 責務: 裏返し結果のキャッシュ機能を提供する
/// 入出力: GameContext → BoardContext（キャッシュされた盤面状態）
/// 副作用: Add/Dispose で内部キャッシュを更新
///
/// 設計方針:
/// - Cache の各要素は絶対に null にしない（NullReferenceException 防止）
/// - コンストラクタで全スロット（0〜Stage.Max）を初期化
/// - Dispose では Clear() のみ実行（null 代入禁止）
/// </summary>
using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Models;

namespace Reluca.Cachers
{
#pragma warning disable CS8601

    /// <summary>
    /// 裏返し結果のキャッシュ機能を提供します。
    /// </summary>
    public class ReverseResultCacher : ICacheable<GameContext, BoardContext>
    {
        /// <summary>
        /// キャッシュ（各要素は絶対に null にならない）
        /// </summary>
        private Dictionary<int, Dictionary<string, BoardContext>> Cache { get; set; }

        /// <summary>
        /// コンストラクタ。全スロットを初期化します。
        /// </summary>
        public ReverseResultCacher()
        {
            Cache = new Dictionary<int, Dictionary<string, BoardContext>>();
            // 0 から Stage.Max まで全スロットを初期化（null になることを防止）
            for (var i = 0; i <= Stage.Max; i++)
            {
                Cache[i] = new Dictionary<string, BoardContext>();
            }
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="value">盤面状態</param>
        public void Add(GameContext context, BoardContext value)
        {
            Cache[context.Stage][GenerateKey(context)] = value;
        }

        /// <summary>
        /// キャッシュを追加します。
        /// </summary>
        /// <param name="move">指し手</param>
        /// <param name="context">ゲーム状態</param>
        /// <param name="value">盤面状態</param>
        public void Add(int move, GameContext context, BoardContext value)
        {
            Cache[context.Stage][GenerateKey(move, context)] = BoardAccessor.DeepCopy(value);
        }

        /// <summary>
        /// 指定ステージより前のキャッシュをクリアします。
        /// null 代入ではなく Clear() を使用します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        public void Dispose(GameContext context)
        {
            for (var i = 0; i < context.Stage; i++)
            {
                Cache[i].Clear();
            }
        }

        /// <summary>
        /// キャッシュを取得します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>盤面状態</returns>
        public BoardContext Get(GameContext context)
        {
            return Cache[context.Stage][GenerateKey(context)];
        }

        /// <summary>
        /// キャッシュを取得するとともに、取得できたかどうかの結果を返却します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <param name="value">盤面状態</param>
        /// <returns>キャッシュが取得できたかどうか</returns>
        public bool TryGet(GameContext context, out BoardContext value)
        {
            return Cache[context.Stage].TryGetValue(GenerateKey(context), out value);
        }

        /// <summary>
        /// キーを生成します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>キー文字列</returns>
        private static string GenerateKey(GameContext context)
        {
            return $"{context.Move}|{context.Black}|{context.White}";
        }

        /// <summary>
        /// キーを生成します。
        /// </summary>
        /// <param name="move">指し手</param>
        /// <param name="context">ゲーム状態</param>
        /// <returns>キー文字列</returns>
        private static string GenerateKey(int move, GameContext context)
        {
            return $"{move}|{context.Black}|{context.White}";
        }
    }
}
