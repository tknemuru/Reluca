/// <summary>
/// 【ModuleDoc】
/// 責務: 置換表の設定パラメータを保持する
/// 入出力: なし（設定データ保持のみ）
/// 副作用: なし
///
/// サイズ制約:
/// - TableSize は 2 の累乗であること（高速インデックス計算のため）
/// - 2 の累乗でない値が渡された場合は、最近傍の 2 の累乗に切り上げる
/// - 理由: index = key & (size - 1) の高速なビットマスク演算を使用するため
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// 置換表の設定を保持するクラスです。
    /// DIコンテナから Singleton として取得されます。
    /// </summary>
    public class TranspositionTableConfig
    {
        /// <summary>
        /// デフォルトのテーブルサイズ（2^20 = 約100万エントリ）。
        /// </summary>
        public const int DefaultTableSize = 1 << 20;

        /// <summary>
        /// 最小のテーブルサイズ（2^10 = 1024エントリ）。
        /// </summary>
        public const int MinTableSize = 1 << 10;

        /// <summary>
        /// 置換表のエントリ数。
        /// 2 の累乗であることが保証されます。
        /// </summary>
        public int TableSize { get; }

        /// <summary>
        /// デフォルト設定でインスタンスを作成します。
        /// </summary>
        public TranspositionTableConfig() : this(DefaultTableSize)
        {
        }

        /// <summary>
        /// 指定されたサイズでインスタンスを作成します。
        /// サイズが 2 の累乗でない場合は、最近傍の 2 の累乗に切り上げられます。
        /// </summary>
        /// <param name="tableSize">希望するテーブルサイズ</param>
        public TranspositionTableConfig(int tableSize)
        {
            if (tableSize < MinTableSize)
            {
                tableSize = MinTableSize;
            }

            TableSize = RoundUpToPowerOfTwo(tableSize);
        }

        /// <summary>
        /// 指定された値を 2 の累乗に切り上げます。
        /// 既に 2 の累乗の場合はそのまま返します。
        /// </summary>
        /// <param name="value">入力値</param>
        /// <returns>2 の累乗に切り上げた値</returns>
        private static int RoundUpToPowerOfTwo(int value)
        {
            // 既に 2 の累乗かチェック
            if ((value & (value - 1)) == 0)
            {
                return value;
            }

            // 最上位ビットを見つけて次の 2 の累乗を計算
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }
    }
}
