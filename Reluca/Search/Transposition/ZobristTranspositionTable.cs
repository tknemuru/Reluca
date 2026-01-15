/// <summary>
/// 【ModuleDoc】
/// 責務: Zobrist ハッシュを使用した置換表を実装する
/// 入出力: ハッシュキー/探索情報 ⇔ TTEntry
/// 副作用: Store 時にテーブル内容を更新、Clear 時にテーブルを初期化
///
/// 実装詳細:
/// - 配列ベースのハッシュテーブル（サイズは 2 の累乗）
/// - インデックス計算: index = key & (size - 1) で高速化
/// - 置換戦略: Depth-Preferred（より深い探索結果を優先）
/// - 衝突対策: TTEntry に Key を保持し、Probe 時に一致を確認
/// - スレッドセーフティ: Task 2 では未対応（後続タスクで検討）
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// Zobrist ハッシュを使用した置換表の実装クラスです。
    /// DIコンテナから Singleton として取得されます。
    /// </summary>
    public class ZobristTranspositionTable : ITranspositionTable
    {
        /// <summary>
        /// エントリを格納する配列。
        /// </summary>
        private readonly TTEntry[] _entries;

        /// <summary>
        /// テーブルサイズ（2 の累乗）。
        /// </summary>
        private readonly int _size;

        /// <summary>
        /// インデックス計算用のマスク（size - 1）。
        /// key & _indexMask で高速にインデックスを計算できます。
        /// </summary>
        private readonly ulong _indexMask;

        /// <summary>
        /// 設定からインスタンスを作成します。
        /// </summary>
        /// <param name="config">置換表の設定</param>
        public ZobristTranspositionTable(TranspositionTableConfig config)
        {
            _size = config.TableSize;
            _indexMask = (ulong)(_size - 1);
            _entries = new TTEntry[_size];

            // 初期状態で全エントリをクリア
            Clear();
        }

        /// <summary>
        /// 指定されたキーに対応するインデックスを計算します。
        /// </summary>
        /// <param name="key">Zobrist ハッシュキー</param>
        /// <returns>配列インデックス</returns>
        private int GetIndex(ulong key)
        {
            return (int)(key & _indexMask);
        }

        /// <inheritdoc/>
        public void Store(ulong key, int depth, long value, BoundType bound, int bestMove)
        {
            int index = GetIndex(key);
            ref TTEntry existing = ref _entries[index];

            // Depth-Preferred: 既存エントリより深い場合のみ置換
            // 同じキーの場合は常に更新（同一局面のより深い探索結果）
            // 異なるキーの場合は深さで判断
            if (existing.Key == key || depth >= existing.Depth)
            {
                existing = new TTEntry(key, depth, value, bound, bestMove);
            }
        }

        /// <inheritdoc/>
        public bool TryProbe(ulong key, int requestedDepth, long alpha, long beta, out TTEntry entry)
        {
            int index = GetIndex(key);
            entry = _entries[index];

            // キーが一致しない場合はミス
            if (entry.Key != key)
            {
                entry = default;
                return false;
            }

            // 深さが不十分な場合はミス
            if (entry.Depth < requestedDepth)
            {
                entry = default;
                return false;
            }

            // Bound 条件をチェック
            switch (entry.Bound)
            {
                case BoundType.Exact:
                    // 正確な値は常に有効
                    return true;

                case BoundType.LowerBound:
                    // 下界は value >= beta の場合のみカットオフ可能
                    if (entry.Value >= beta)
                    {
                        return true;
                    }
                    break;

                case BoundType.UpperBound:
                    // 上界は value <= alpha の場合のみカットオフ可能
                    if (entry.Value <= alpha)
                    {
                        return true;
                    }
                    break;
            }

            // Bound 条件を満たさない場合はミス
            entry = default;
            return false;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            for (int i = 0; i < _size; i++)
            {
                _entries[i].Clear();
            }
        }

        /// <inheritdoc/>
        public int GetBestMove(ulong key)
        {
            int index = GetIndex(key);
            ref TTEntry entry = ref _entries[index];

            // キーが一致する場合のみ最善手を返す
            if (entry.Key == key)
            {
                return entry.BestMove;
            }

            return TTEntry.NoBestMove;
        }
    }
}
