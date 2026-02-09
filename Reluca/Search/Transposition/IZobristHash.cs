/// <summary>
/// 【ModuleDoc】
/// 責務: Zobrist ハッシュ計算のインターフェースを定義する
/// 入出力: GameContext → ulong ハッシュ値
/// 副作用: なし
///
/// 備考:
/// - ComputeHash: 盤面全体からハッシュ値をフルスキャン計算
/// - UpdateHash: 着手による差分のみで O(popcount(flipped)) のハッシュ更新
/// </summary>
using Reluca.Contexts;

namespace Reluca.Search.Transposition
{
    /// <summary>
    /// Zobrist ハッシュ計算のインターフェースです。
    /// 盤面状態から一意のハッシュ値を生成し、置換表のキーとして使用します。
    /// </summary>
    public interface IZobristHash
    {
        /// <summary>
        /// 指定されたゲーム状態から Zobrist ハッシュ値を計算します。
        /// 盤面上の全ての石と手番を考慮してハッシュ値を生成します。
        /// </summary>
        /// <param name="context">ゲーム状態</param>
        /// <returns>計算されたハッシュ値</returns>
        ulong ComputeHash(GameContext context);

        /// <summary>
        /// 着手による差分で Zobrist ハッシュ値を更新します。
        /// 着手位置に自石を配置し、裏返された石の色を反転させ、手番を切り替えます。
        /// XOR の自己逆元性を利用し、変化したマスのみ O(popcount(flipped)) で更新します。
        /// </summary>
        /// <param name="currentHash">現在のハッシュ値</param>
        /// <param name="move">着手位置（0-63）</param>
        /// <param name="flipped">裏返された石のビットボード</param>
        /// <param name="isBlackTurn">着手側が黒番であるかどうか（着手前の手番）</param>
        /// <returns>更新後のハッシュ値</returns>
        ulong UpdateHash(ulong currentHash, int move, ulong flipped, bool isBlackTurn);
    }
}
