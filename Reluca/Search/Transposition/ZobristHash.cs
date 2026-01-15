/// <summary>
/// 【ModuleDoc】
/// 責務: GameContext から Zobrist ハッシュ値を計算する
/// 入出力: GameContext → ulong ハッシュ値
/// 副作用: なし
///
/// 備考:
/// - Task 3c での差分更新実装を見据え、インスタンスクラスに変更
/// - 現状は ComputeHash でフルスキャン計算のみを提供
/// </summary>
using Reluca.Contexts;
using Reluca.Models;

namespace Reluca.Search.Transposition
{
    /// <summary>
    /// Zobrist ハッシュを計算するクラスです。
    /// 盤面状態と手番から一意のハッシュ値を生成し、置換表のキーとして使用します。
    /// </summary>
    public class ZobristHash : IZobristHash
    {
        /// <summary>
        /// 盤面のマス数（8×8）。
        /// </summary>
        private const int BoardSize = 64;

        /// <summary>
        /// 指定されたゲーム状態から Zobrist ハッシュ値を計算します。
        /// 盤面上の全ての石と手番を考慮してハッシュ値を生成します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>計算されたハッシュ値</returns>
        public ulong ComputeHash(GameContext context)
        {
            ulong hash = 0;

            ulong blackBoard = context.Black;
            ulong whiteBoard = context.White;

            // 各マスの石をチェックしてハッシュを計算
            for (int square = 0; square < BoardSize; square++)
            {
                ulong mask = 1UL << square;

                if ((blackBoard & mask) != 0)
                {
                    // 黒石がある
                    hash ^= ZobristKeys.PieceKeys[square, ZobristKeys.BlackIndex];
                }
                else if ((whiteBoard & mask) != 0)
                {
                    // 白石がある
                    hash ^= ZobristKeys.PieceKeys[square, ZobristKeys.WhiteIndex];
                }
            }

            // 白番の場合は手番キーを XOR
            if (context.Turn == Disc.Color.White)
            {
                hash ^= ZobristKeys.TurnKey;
            }

            return hash;
        }
    }
}
