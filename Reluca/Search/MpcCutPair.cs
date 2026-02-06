/// <summary>
/// 【ModuleDoc】
/// 責務: Multi-ProbCut のカットペア定義を保持するデータクラス
/// 入出力: なし（データ保持のみ）
/// 副作用: なし
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// Multi-ProbCut のカットペア定義を保持する。
    /// 浅い探索深さ（ShallowDepth）と深い探索深さ（DeepDepth）の組み合わせを表す。
    /// </summary>
    public class MpcCutPair
    {
        /// <summary>
        /// 浅い探索深さ
        /// </summary>
        public int ShallowDepth { get; }

        /// <summary>
        /// 深い探索深さ（適用条件: remainingDepth >= DeepDepth）
        /// </summary>
        public int DeepDepth { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="shallowDepth">浅い探索深さ</param>
        /// <param name="deepDepth">深い探索深さ</param>
        public MpcCutPair(int shallowDepth, int deepDepth)
        {
            ShallowDepth = shallowDepth;
            DeepDepth = deepDepth;
        }
    }
}
