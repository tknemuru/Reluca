/// <summary>
/// 【ModuleDoc】
/// 責務: 対局全体の持ち時間を各手番に配分する
/// 入出力: 残り持ち時間 + ターン数 → 今回の手番に割り当てる制限時間
/// 副作用: なし
///
/// 状態を持たない純粋な計算クラスであり、すべてのメソッドは入力パラメータのみに基づいて結果を返す。
/// DI コンテナへの登録は行わず、利用側で直接インスタンス化する。
///
/// 配分戦略:
/// - 序盤（ターン 0〜15）: 係数 0.8（短め。定石による知識で対応できるため）
/// - 中盤（ターン 16〜44）: 係数 1.3（長め。局面の複雑度が最も高い）
/// - 終盤（ターン 45〜59）: 係数 0.9（やや短め。完全読み切りに時間を割きすぎない）
/// - 残り時間の 5% を安全マージンとして確保する
/// - どの局面でも最低 100ms は確保する
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// 対局全体の持ち時間を各手番に配分する。
    /// 残り手数に応じた動的な配分戦略を実装する。
    /// 状態を持たない純粋な計算クラスである。
    /// </summary>
    public class TimeAllocator
    {
        /// <summary>
        /// 最大ターン数（オセロは最大 60 手）
        /// </summary>
        private const int MaxTurns = 60;

        /// <summary>
        /// 最低保証時間（ミリ秒）。どの局面でも最低この時間は確保する
        /// </summary>
        private const long MinTimeLimitMs = 100;

        /// <summary>
        /// 安全マージン比率。残り時間のこの割合を予備として確保する
        /// </summary>
        private const double SafetyMarginRatio = 0.05;

        /// <summary>
        /// 残り持ち時間と現在のターン数から、今回の手番に割り当てる制限時間を計算する。
        /// </summary>
        /// <param name="remainingTimeMs">残り持ち時間（ミリ秒）。0 以下の場合は MinTimeLimitMs を返す</param>
        /// <param name="turnCount">現在のターン数（0〜59）</param>
        /// <returns>今回の手番に割り当てる制限時間（ミリ秒）</returns>
        public long Allocate(long remainingTimeMs, int turnCount)
        {
            if (remainingTimeMs <= 0)
            {
                return MinTimeLimitMs;
            }

            int remainingMoves = EstimateRemainingMoves(turnCount);

            if (remainingMoves <= 0)
            {
                return MinTimeLimitMs;
            }

            // 安全マージンを差し引いた利用可能時間
            long availableMs = (long)(remainingTimeMs * (1.0 - SafetyMarginRatio));

            // フェーズ係数: 中盤で多く、序盤・終盤で少なく配分する
            double phaseWeight = CalculatePhaseWeight(turnCount);

            // 基本配分 = 利用可能時間 / 残り手数
            double baseAllocation = (double)availableMs / remainingMoves;

            // フェーズ補正を適用
            long allocatedMs = (long)(baseAllocation * phaseWeight);

            // 最低保証時間を確保
            return Math.Max(allocatedMs, MinTimeLimitMs);
        }

        /// <summary>
        /// 残り手数を推定する。自手番のみをカウントし、パスの影響を考慮して保守的に推定する。
        /// </summary>
        /// <remarks>
        /// オセロでは一方がパスすると相手が連続して手番を得るため、自分の実際の手番数は
        /// 単純な (残りターン数) / 2 とは異なる場合がある。最悪ケースとして、相手が
        /// 複数回連続パスすることで自分の手番が増加するシナリオが考えられる。
        /// ただし連続パスは終盤に集中し、その場合は残りマス数自体が少ないため探索時間への
        /// 影響は限定的である。本推定式は (remaining + 1) / 2 により切り上げ方向で
        /// 推定しており、1 手分の余裕を持たせることでパスによる手番増加に対して安全側に
        /// 機能する。加えて SafetyMarginRatio（5%）が追加の安全バッファとして作用する。
        /// </remarks>
        /// <param name="turnCount">現在のターン数</param>
        /// <returns>推定残り手数</returns>
        private static int EstimateRemainingMoves(int turnCount)
        {
            int remaining = MaxTurns - turnCount;

            // 自分の手番のみをカウント（2 で割る、切り上げ）
            return Math.Max((remaining + 1) / 2, 1);
        }

        /// <summary>
        /// 現在のターン数に応じたフェーズ係数を計算する。
        /// 序盤（ターン 0〜15）: 0.8 倍（短め）
        /// 中盤（ターン 16〜44）: 1.3 倍（長め）
        /// 終盤（ターン 45〜59）: 0.9 倍（やや短め。完全読み切りに時間を割きすぎない）
        /// </summary>
        /// <param name="turnCount">現在のターン数</param>
        /// <returns>フェーズ係数</returns>
        private static double CalculatePhaseWeight(int turnCount)
        {
            if (turnCount <= 15)
            {
                return 0.8;
            }
            else if (turnCount <= 44)
            {
                return 1.3;
            }
            else
            {
                return 0.9;
            }
        }
    }
}
