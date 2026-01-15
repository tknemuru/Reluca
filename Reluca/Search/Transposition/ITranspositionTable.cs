/// <summary>
/// 【ModuleDoc】
/// 責務: 置換表のインターフェースを定義する
/// 入出力: ハッシュキー/探索情報 ⇔ TTEntry
/// 副作用: Store 時にテーブル内容を更新
///
/// TryProbe 仕様:
/// 1. キーの一致チェック: entry.Key == key でなければミス
/// 2. 深さチェック: entry.Depth >= requestedDepth でなければミス
/// 3. Bound 条件チェック:
///    - Exact: 常に有効（true を返す）
///    - LowerBound: entry.Value >= beta の場合のみ有効
///    - UpperBound: entry.Value <= alpha の場合のみ有効
/// 4. 上記すべてを満たす場合のみ true を返し、entry に値を設定
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// 置換表のインターフェースです。
    /// 探索済み局面の情報を保存・取得する機能を提供します。
    /// </summary>
    public interface ITranspositionTable
    {
        /// <summary>
        /// 探索結果を置換表に保存します。
        /// Depth-Preferred 戦略により、既存エントリより深い探索結果のみが保存されます。
        /// </summary>
        /// <param name="key">局面の Zobrist ハッシュキー</param>
        /// <param name="depth">探索深さ</param>
        /// <param name="value">評価値</param>
        /// <param name="bound">境界タイプ</param>
        /// <param name="bestMove">最善手（未設定の場合は TTEntry.NoBestMove）</param>
        void Store(ulong key, int depth, long value, BoundType bound, int bestMove);

        /// <summary>
        /// 置換表から探索結果を取得します。
        /// キーの一致、深さ条件、Bound条件をすべて満たす場合のみ true を返します。
        ///
        /// 条件詳細:
        /// - キー一致: entry.Key == key
        /// - 深さ条件: entry.Depth >= requestedDepth
        /// - Bound条件:
        ///   - Exact: 常に有効
        ///   - LowerBound: entry.Value >= beta の場合のみ有効
        ///   - UpperBound: entry.Value <= alpha の場合のみ有効
        /// </summary>
        /// <param name="key">局面の Zobrist ハッシュキー</param>
        /// <param name="requestedDepth">要求する最小探索深さ</param>
        /// <param name="alpha">現在の alpha 値</param>
        /// <param name="beta">現在の beta 値</param>
        /// <param name="entry">取得されたエントリ（条件を満たさない場合は default）</param>
        /// <returns>有効なエントリが見つかった場合は true</returns>
        bool TryProbe(ulong key, int requestedDepth, long alpha, long beta, out TTEntry entry);

        /// <summary>
        /// 置換表をクリアし、すべてのエントリを初期状態に戻します。
        /// 初期状態: Key=0, Depth=0, Value=0, Bound=Exact, BestMove=-1
        /// </summary>
        void Clear();

        /// <summary>
        /// 指定されたキーに対応する最善手を取得します。
        /// 手順序の改善（Move Ordering）に使用されます。
        /// キーが存在しない、またはキーが一致しない場合は TTEntry.NoBestMove (-1) を返します。
        /// </summary>
        /// <param name="key">局面の Zobrist ハッシュキー</param>
        /// <returns>最善手のマス番号（0-63）、存在しない場合は -1</returns>
        int GetBestMove(ulong key);
    }
}
