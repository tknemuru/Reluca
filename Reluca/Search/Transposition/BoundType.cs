/// <summary>
/// 【ModuleDoc】
/// 責務: 置換表エントリの評価値の境界タイプを定義する
/// 入出力: なし（列挙型定義のみ）
/// 副作用: なし
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// 置換表エントリに格納された評価値の境界タイプを表します。
    /// アルファベータ探索において、カットオフの種類によって異なる境界タイプが設定されます。
    /// </summary>
    public enum BoundType
    {
        /// <summary>
        /// 正確な評価値。
        /// 探索窓内で最善手が見つかり、完全に評価が確定した場合に設定されます。
        /// </summary>
        Exact = 0,

        /// <summary>
        /// 下界（評価値以上であることが保証される）。
        /// ベータカットオフが発生し、実際の評価値は格納値以上である場合に設定されます。
        /// </summary>
        LowerBound = 1,

        /// <summary>
        /// 上界（評価値以下であることが保証される）。
        /// すべての手を探索しても alpha を更新できなかった場合に設定されます。
        /// </summary>
        UpperBound = 2
    }
}
