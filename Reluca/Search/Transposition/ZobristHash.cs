/// <summary>
/// 【ModuleDoc】
/// 責務: GameContext から Zobrist ハッシュ値を計算する
/// 入出力: GameContext → ulong ハッシュ値
/// 副作用: なし
///
/// 備考:
/// - ComputeHash: フルスキャン計算（初回ハッシュ生成用）
/// - UpdateHash: 差分更新（探索中の MakeMove/UnmakeMove 時に使用）
/// </summary>
using Reluca.Contexts;
using Reluca.Models;
using System.Numerics;

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

        /// <summary>
        /// 着手による差分で Zobrist ハッシュ値を更新します。
        /// XOR の自己逆元性（A ^ B ^ B = A）を利用し、変化したマスのみ更新します。
        /// 計算量は O(popcount(flipped)) で、フルスキャンの O(64) より高速です。
        /// </summary>
        /// <param name="currentHash">現在のハッシュ値</param>
        /// <param name="move">着手位置（0-63）</param>
        /// <param name="flipped">裏返された石のビットボード</param>
        /// <param name="isBlackTurn">着手側が黒番であるかどうか（着手前の手番）</param>
        /// <returns>更新後のハッシュ値</returns>
        public ulong UpdateHash(ulong currentHash, int move, ulong flipped, bool isBlackTurn)
        {
            ulong hash = currentHash;

            // 着手側と相手側の色インデックスを決定
            int playerColorIndex = isBlackTurn ? ZobristKeys.BlackIndex : ZobristKeys.WhiteIndex;
            int opponentColorIndex = isBlackTurn ? ZobristKeys.WhiteIndex : ZobristKeys.BlackIndex;

            // 1. 着手位置に自石を配置（空→自石）
            hash ^= ZobristKeys.PieceKeys[move, playerColorIndex];

            // 2. 裏返された石: 相手色を除去して自色を追加（XOR で色を反転）
            ulong remaining = flipped;
            while (remaining != 0)
            {
                int square = BitOperations.TrailingZeroCount(remaining);
                hash ^= ZobristKeys.PieceKeys[square, opponentColorIndex]; // 相手色を除去
                hash ^= ZobristKeys.PieceKeys[square, playerColorIndex];   // 自色を追加
                remaining &= remaining - 1; // 最下位ビットをクリア
            }

            // 3. 手番切り替え（常に XOR で反転）
            hash ^= ZobristKeys.TurnKey;

            return hash;
        }
    }
}
