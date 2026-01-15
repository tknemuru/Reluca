/// <summary>
/// 【ModuleDoc】
/// 責務: Zobrist ハッシュ計算に使用する乱数テーブルを提供する
/// 入出力: なし（静的データ提供のみ）
/// 副作用: なし
///
/// 乱数生成方法:
/// - 固定シード (12345) を使用した System.Random で生成
/// - 再現性を担保するため、シードは変更しないこと
/// - 64マス × 2色（黒=0, 白=1）の乱数テーブルと手番用乱数を静的に保持
/// </summary>
namespace Reluca.Search.Transposition
{
    /// <summary>
    /// Zobrist ハッシュ計算に使用する乱数キーを提供する静的クラスです。
    /// 固定シードで生成された乱数を使用し、同一局面は常に同一ハッシュ値となることを保証します。
    /// </summary>
    public static class ZobristKeys
    {
        /// <summary>
        /// 乱数生成に使用する固定シード。
        /// 再現性を担保するため、この値は変更しないでください。
        /// </summary>
        private const int RandomSeed = 12345;

        /// <summary>
        /// 盤面のマス数（8×8）。
        /// </summary>
        private const int BoardSize = 64;

        /// <summary>
        /// 石の色の数（黒と白）。
        /// </summary>
        private const int ColorCount = 2;

        /// <summary>
        /// 黒石のインデックス。
        /// </summary>
        public const int BlackIndex = 0;

        /// <summary>
        /// 白石のインデックス。
        /// </summary>
        public const int WhiteIndex = 1;

        /// <summary>
        /// 各マス・各色に対応する乱数キー。
        /// PieceKeys[square, color] で取得します。
        /// square: 0-63（左上から右下へ行優先）
        /// color: 0=黒, 1=白
        /// </summary>
        public static readonly ulong[,] PieceKeys;

        /// <summary>
        /// 手番が白の場合に XOR する乱数キー。
        /// 同一盤面でも手番が異なればハッシュ値が変わることを保証します。
        /// </summary>
        public static readonly ulong TurnKey;

        /// <summary>
        /// 静的コンストラクタ。固定シードで乱数テーブルを初期化します。
        /// </summary>
        static ZobristKeys()
        {
            var random = new Random(RandomSeed);
            PieceKeys = new ulong[BoardSize, ColorCount];

            // 各マス・各色の乱数を生成
            for (int square = 0; square < BoardSize; square++)
            {
                for (int color = 0; color < ColorCount; color++)
                {
                    PieceKeys[square, color] = GenerateRandomULong(random);
                }
            }

            // 手番用の乱数を生成
            TurnKey = GenerateRandomULong(random);
        }

        /// <summary>
        /// 64ビット乱数を生成します。
        /// System.Random.NextInt64() が利用できない環境向けに、2つの32ビット値を結合します。
        /// </summary>
        /// <param name="random">乱数生成器</param>
        /// <returns>64ビット乱数</returns>
        private static ulong GenerateRandomULong(Random random)
        {
            // 上位32ビットと下位32ビットを別々に生成して結合
            ulong high = (ulong)(uint)random.Next();
            ulong low = (ulong)(uint)random.Next();
            return (high << 32) | low;
        }
    }
}
