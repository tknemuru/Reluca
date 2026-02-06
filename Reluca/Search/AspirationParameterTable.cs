/// <summary>
/// 【ModuleDoc】
/// 責務: Aspiration Window のステージ別パラメータテーブルを管理する
/// 入出力: stage → delta 初期値
/// 副作用: なし
///
/// 設計方針:
/// - Singleton として DI に登録し、探索エンジン間で共有する
/// - ステージ別 delta テーブルは long[] 配列でインデックスアクセス（O(1)）
/// - 深さ補正は整数演算（分子/分母）で浮動小数点演算を回避
/// - 範囲外のステージにはデフォルト delta をフォールバック値として返す
/// </summary>
using Reluca.Models;

namespace Reluca.Search
{
    /// <summary>
    /// Aspiration Window のステージ別パラメータテーブルを管理する。
    /// </summary>
    public class AspirationParameterTable
    {
        /// <summary>
        /// ステージ別の delta 初期値テーブル。
        /// インデックス = stage - 1 で O(1) アクセスする。
        /// </summary>
        private readonly long[] _deltaByStage;

        /// <summary>
        /// デフォルトの delta 初期値。
        /// テーブル範囲外のステージ（stage &lt; 1 または stage &gt; Stage.Max）に対する
        /// フォールバック値として使用される。テーブル内の各ステージの値とは独立して
        /// 管理されるため、テーブル値を調整する際にこの値を連動して変更する必要はない。
        /// </summary>
        public long DefaultDelta { get; }

        /// <summary>
        /// コンストラクタ。デフォルトパラメータで初期化する。
        /// </summary>
        public AspirationParameterTable()
        {
            DefaultDelta = 50;
            _deltaByStage = BuildDefaultTable();
        }

        /// <summary>
        /// 指定ステージの delta 初期値を取得する。
        /// </summary>
        /// <param name="stage">ゲームステージ（1〜15）</param>
        /// <returns>delta 初期値</returns>
        public long GetDelta(int stage)
        {
            int index = stage - 1;
            if (index >= 0 && index < _deltaByStage.Length)
            {
                return _deltaByStage[index];
            }
            return DefaultDelta;
        }

        /// <summary>
        /// 深さ補正を適用した delta を取得する。
        /// 浅い深さでは delta を大きくし、深い深さではそのまま返す。
        /// 浮動小数点演算を回避するため、整数演算（分子/分母）で補正を行う。
        /// </summary>
        /// <param name="baseDelta">ステージ別の基本 delta</param>
        /// <param name="depth">探索深さ</param>
        /// <returns>補正後の delta</returns>
        public static long GetAdjustedDelta(long baseDelta, int depth)
        {
            if (depth <= 2) return baseDelta * 2;       // 2.0 倍
            if (depth <= 4) return baseDelta * 3 / 2;   // 1.5 倍（整数演算: 端数は切り捨て）
            return baseDelta;                             // 1.0 倍（補正なし）
        }

        /// <summary>
        /// デフォルトのステージ別 delta テーブルを構築する。
        /// </summary>
        /// <returns>ステージ別 delta 配列（インデックス = stage - 1）</returns>
        private long[] BuildDefaultTable()
        {
            var table = new long[Stage.Max]; // Stage.Max = 15

            for (int stage = 1; stage <= Stage.Max; stage++)
            {
                if (stage <= 5)
                {
                    // 序盤: 評価値の変動が大きい → delta を大きめに設定
                    table[stage - 1] = 80;
                }
                else if (stage <= 10)
                {
                    // 中盤: 評価値が安定してくる → 標準的な delta
                    table[stage - 1] = 50;
                }
                else
                {
                    // 終盤: 評価値が安定 → delta を小さめに設定
                    table[stage - 1] = 30;
                }
            }

            return table;
        }
    }
}
