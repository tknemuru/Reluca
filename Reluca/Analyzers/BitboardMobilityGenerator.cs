using System.Numerics;

namespace Reluca.Analyzers
{
    /// <summary>
    /// ビットボード演算による高速合法手生成機能を提供する静的クラスです。
    /// 8 方向のシフト演算とマスク操作により、全合法手を一括で算出します。
    ///
    /// マスク適用方針:
    /// 列成分を持つ方向（East/West/SE/NW/SW/NE）では、シフト操作による
    /// 列の回り込み（H列→A列、A列→H列）を防止するためにマスクを使用する。
    /// マスクは以下の 3 箇所すべてに適用する:
    /// 1. maskedOpponent: 中間ステップでの回り込みを防止
    /// 2. FindFlips の最終結果: 最後のシフトで回り込んだ偽の合法手を除去
    /// 3. ComputeFlippedDirection の自石確認: 回り込んだ位置での偽の挟み判定を防止
    /// </summary>
    public static class BitboardMobilityGenerator
    {
        /// <summary>
        /// A列（左端）のビットを除外するマスク。
        /// 右方向成分を持つシフト（E, SE, NE）で H列→A列への回り込みを防止します。
        /// </summary>
        private const ulong NotAFile = 0xFEFEFEFEFEFEFEFEUL;

        /// <summary>
        /// H列（右端）のビットを除外するマスク。
        /// 左方向成分を持つシフト（W, NW, SW）で A列→H列への回り込みを防止します。
        /// </summary>
        private const ulong NotHFile = 0x7F7F7F7F7F7F7F7FUL;

        /// <summary>
        /// 全ビット有効マスク。
        /// 上下方向のシフトでは列の回り込みが発生しないため、マスク不要です。
        /// </summary>
        private const ulong AllBits = 0xFFFFFFFFFFFFFFFFUL;

        /// <summary>
        /// 指定された手番の全合法手をビットボードとして返します。
        /// 戻り値の各ビットが 1 であるマスが合法手です。
        /// </summary>
        /// <param name="player">手番側の石の配置</param>
        /// <param name="opponent">相手側の石の配置</param>
        /// <returns>合法手のビットボード</returns>
        public static ulong GenerateMoves(ulong player, ulong opponent)
        {
            ulong empty = ~(player | opponent);
            ulong moves = 0UL;

            // 8方向: 右, 左, 下, 上, 右下, 左上, 左下, 右上
            // マスク選定基準: 各方向の列成分によるシフト操作で発生する列の回り込みを防止する
            // 右方向成分（E, SE, NE）: H列(col7)→A列(col0)回り込み → NotAFile で A列を除外
            // 左方向成分（W, NW, SW）: A列(col0)→H列(col7)回り込み → NotHFile で H列を除外
            // 行方向のみ（S, N）: 列の回り込みなし → AllBits
            moves |= FindFlips(player, opponent, empty, 1, NotAFile);    // 右 (East):  右成分 → H列→A列回り込み防止
            moves |= FindFlips(player, opponent, empty, -1, NotHFile);   // 左 (West):  左成分 → A列→H列回り込み防止
            moves |= FindFlips(player, opponent, empty, 8, AllBits);     // 下 (South): 行方向のみ → 回り込みなし
            moves |= FindFlips(player, opponent, empty, -8, AllBits);    // 上 (North): 行方向のみ → 回り込みなし
            moves |= FindFlips(player, opponent, empty, 9, NotAFile);    // 右下 (SE):  右成分 → H列→A列回り込み防止
            moves |= FindFlips(player, opponent, empty, -9, NotHFile);   // 左上 (NW):  左成分 → A列→H列回り込み防止
            moves |= FindFlips(player, opponent, empty, 7, NotHFile);    // 左下 (SW):  左成分 → A列→H列回り込み防止
            moves |= FindFlips(player, opponent, empty, -7, NotAFile);   // 右上 (NE):  右成分 → H列→A列回り込み防止

            return moves;
        }

        /// <summary>
        /// 指定方向について、相手石の連続を越えた先の空マスを合法手として検出します。
        /// 最終シフト結果にもマスクを適用し、列の回り込みによる偽の合法手を防止します。
        /// </summary>
        /// <param name="player">手番側の石の配置</param>
        /// <param name="opponent">相手石の配置</param>
        /// <param name="empty">空マス</param>
        /// <param name="shift">シフト量（正:左シフト, 負:右シフト）</param>
        /// <param name="mask">列の回り込み防止マスク</param>
        /// <returns>この方向での合法手ビットボード</returns>
        private static ulong FindFlips(ulong player, ulong opponent, ulong empty,
                                        int shift, ulong mask)
        {
            ulong maskedOpponent = opponent & mask;
            ulong flipped;

            if (shift > 0)
            {
                flipped = maskedOpponent & (player << shift);
                flipped |= maskedOpponent & (flipped << shift);
                flipped |= maskedOpponent & (flipped << shift);
                flipped |= maskedOpponent & (flipped << shift);
                flipped |= maskedOpponent & (flipped << shift);
                flipped |= maskedOpponent & (flipped << shift);
                return empty & mask & (flipped << shift);
            }
            else
            {
                int rshift = -shift;
                flipped = maskedOpponent & (player >> rshift);
                flipped |= maskedOpponent & (flipped >> rshift);
                flipped |= maskedOpponent & (flipped >> rshift);
                flipped |= maskedOpponent & (flipped >> rshift);
                flipped |= maskedOpponent & (flipped >> rshift);
                flipped |= maskedOpponent & (flipped >> rshift);
                return empty & mask & (flipped >> rshift);
            }
        }

        /// <summary>
        /// 合法手のビットボードから合法手の数を返します。
        /// </summary>
        /// <param name="moves">合法手のビットボード</param>
        /// <returns>合法手の数</returns>
        public static int CountMoves(ulong moves)
        {
            return BitOperations.PopCount(moves);
        }

        /// <summary>
        /// 合法手のビットボードから合法手のインデックスリストを生成します。
        /// </summary>
        /// <param name="moves">合法手のビットボード</param>
        /// <returns>合法手のインデックスリスト</returns>
        public static List<int> ToMoveList(ulong moves)
        {
            var list = new List<int>(BitOperations.PopCount(moves));
            while (moves != 0)
            {
                int index = BitOperations.TrailingZeroCount(moves);
                list.Add(index);
                moves &= moves - 1; // 最下位ビットをクリア
            }
            return list;
        }

        /// <summary>
        /// 指定位置に着手した場合に裏返される石のビットボードを計算します。
        /// </summary>
        /// <param name="player">手番側の石の配置</param>
        /// <param name="opponent">相手側の石の配置</param>
        /// <param name="move">着手位置（0-63）</param>
        /// <returns>裏返される石のビットボード</returns>
        public static ulong ComputeFlipped(ulong player, ulong opponent, int move)
        {
            ulong moveBit = 1UL << move;
            ulong flipped = 0UL;

            // 8方向それぞれについて裏返し判定（マスク対応は GenerateMoves と同一）
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, 1, NotAFile);    // 右 (East)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, -1, NotHFile);   // 左 (West)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, 8, AllBits);     // 下 (South)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, -8, AllBits);    // 上 (North)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, 9, NotAFile);    // 右下 (SE)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, -9, NotHFile);   // 左上 (NW)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, 7, NotHFile);    // 左下 (SW)
            flipped |= ComputeFlippedDirection(player, opponent, moveBit, -7, NotAFile);   // 右上 (NE)

            return flipped;
        }

        /// <summary>
        /// 指定方向について、着手位置から相手石の連続を追跡し、
        /// その先に自石がある場合に裏返される石のビットボードを返します。
        /// 自石確認の最終シフトにもマスクを適用し、列の回り込みによる偽の挟み判定を防止します。
        /// </summary>
        /// <param name="player">手番側の石の配置</param>
        /// <param name="opponent">相手石の配置</param>
        /// <param name="moveBit">着手位置のビットマスク</param>
        /// <param name="shift">シフト量（正:左シフト, 負:右シフト）</param>
        /// <param name="mask">列の回り込み防止マスク</param>
        /// <returns>この方向で裏返される石のビットボード</returns>
        private static ulong ComputeFlippedDirection(ulong player, ulong opponent,
                                                      ulong moveBit, int shift, ulong mask)
        {
            ulong maskedOpponent = opponent & mask;
            ulong flipped = 0UL;
            ulong cursor;

            if (shift > 0)
            {
                cursor = maskedOpponent & (moveBit << shift);
                while (cursor != 0)
                {
                    flipped |= cursor;
                    cursor = maskedOpponent & (cursor << shift);
                }
                // 連続の先に自石があるか確認（マスク適用で回り込み防止）
                if ((player & mask & (flipped << shift)) == 0)
                {
                    return 0UL; // 自石で挟まれていないため裏返し不成立
                }
            }
            else
            {
                int rshift = -shift;
                cursor = maskedOpponent & (moveBit >> rshift);
                while (cursor != 0)
                {
                    flipped |= cursor;
                    cursor = maskedOpponent & (cursor >> rshift);
                }
                // 連続の先に自石があるか確認（マスク適用で回り込み防止）
                if ((player & mask & (flipped >> rshift)) == 0)
                {
                    return 0UL; // 自石で挟まれていないため裏返し不成立
                }
            }

            return flipped;
        }
    }
}
