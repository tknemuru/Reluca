/// <summary>
/// 【ModuleDoc】
/// 責務: Zobrist ハッシュ計算のインターフェースを定義する
/// 入出力: GameContext → ulong ハッシュ値
/// 副作用: なし
///
/// 備考:
/// - Task 3c での差分更新実装を見据えたインターフェース抽出
/// - 現状は ComputeHash のみ、将来的に UpdateHash を追加予定
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
    }
}
