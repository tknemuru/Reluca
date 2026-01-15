/// <summary>
/// 【ModuleDoc】
/// 責務: 置換表の1エントリを表す構造体を定義する
/// 入出力: なし（データ構造定義のみ）
/// 副作用: なし
///
/// 初期状態の定義:
/// - Key = 0
/// - Depth = 0
/// - Value = 0
/// - Bound = BoundType.Exact
/// - BestMove = -1（未設定を表す）
///
/// Clear() 後も同じ初期状態に戻ります。
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// 置換表の1エントリを表す構造体です。
    /// 探索済みの局面情報（ハッシュキー、探索深さ、評価値、境界タイプ、最善手）を保持します。
    /// </summary>
    public struct TTEntry
    {
        /// <summary>
        /// 未設定の手を表す定数値。
        /// BestMove がこの値の場合、有効な手が設定されていないことを示します。
        /// </summary>
        public const int NoBestMove = -1;

        /// <summary>
        /// 局面を識別する Zobrist ハッシュキー。
        /// 衝突検出に使用し、Probe 時にキーが一致しない場合はミス扱いとなります。
        /// </summary>
        public ulong Key { get; set; }

        /// <summary>
        /// このエントリが記録された時の探索深さ。
        /// Probe 時に要求深さと比較し、十分な深さがあるかを判定します。
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// 探索によって得られた評価値。
        /// Bound タイプに応じて、正確な値、下界、または上界を表します。
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// 評価値の境界タイプ。
        /// Exact/LowerBound/UpperBound のいずれかで、Probe 時のカットオフ判定に使用されます。
        /// </summary>
        public BoundType Bound { get; set; }

        /// <summary>
        /// この局面での最善手（0-63のマス番号）。
        /// 手順序の改善に使用されます。未設定の場合は NoBestMove (-1) が設定されます。
        /// </summary>
        public int BestMove { get; set; }

        /// <summary>
        /// 指定された値でエントリを初期化します。
        /// </summary>
        /// <param name="key">Zobrist ハッシュキー</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="value">評価値</param>
        /// <param name="bound">境界タイプ</param>
        /// <param name="bestMove">最善手（未設定の場合は NoBestMove）</param>
        public TTEntry(ulong key, int depth, long value, BoundType bound, int bestMove)
        {
            Key = key;
            Depth = depth;
            Value = value;
            Bound = bound;
            BestMove = bestMove;
        }

        /// <summary>
        /// エントリを初期状態にリセットします。
        /// Key=0, Depth=0, Value=0, Bound=Exact, BestMove=NoBestMove となります。
        /// </summary>
        public void Clear()
        {
            Key = 0;
            Depth = 0;
            Value = 0;
            Bound = BoundType.Exact;
            BestMove = NoBestMove;
        }

        /// <summary>
        /// エントリが有効（使用中）かどうかを判定します。
        /// Key が 0 の場合は未使用とみなします。
        /// </summary>
        /// <returns>エントリが有効な場合は true</returns>
        public readonly bool IsValid()
        {
            return Key != 0;
        }
    }
}
