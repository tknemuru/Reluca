/// <summary>
/// 【ModuleDoc】
/// 責務: Multi-ProbCut の回帰パラメータを保持するデータクラス
/// 入出力: なし（データ保持のみ）
/// 副作用: なし
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// Multi-ProbCut の回帰パラメータを保持する。
    /// 浅い探索の評価値から深い探索の評価値を予測する線形回帰モデル
    /// v_d ≈ a * v_d' + b + e（e は平均 0、標準偏差 sigma の正規分布）のパラメータ。
    /// </summary>
    public class MpcParameters
    {
        /// <summary>
        /// 回帰係数（傾き）
        /// </summary>
        public double A { get; }

        /// <summary>
        /// 切片
        /// </summary>
        public double B { get; }

        /// <summary>
        /// 誤差の標準偏差
        /// </summary>
        public double Sigma { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="a">回帰係数</param>
        /// <param name="b">切片</param>
        /// <param name="sigma">標準偏差</param>
        public MpcParameters(double a, double b, double sigma)
        {
            A = a;
            B = b;
            Sigma = sigma;
        }
    }
}
