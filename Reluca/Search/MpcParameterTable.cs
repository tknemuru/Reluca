/// <summary>
/// 【ModuleDoc】
/// 責務: ステージ別・カットペア別の MPC 回帰パラメータテーブルを管理する
/// 入出力: (stage, cutPairIndex) → MpcParameters
/// 副作用: なし
///
/// 設計方針:
/// - Singleton として DI に登録し、探索エンジン間で共有する
/// - 初期パラメータは WZebra 文献ベースの値を手動設定する
/// - ステージ区分（序盤/中盤/終盤）ごとに sigma 値を変動させる
/// - カットペアの深さ差が大きいほど sigma を大きく設定する
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// ステージ別・カットペア別の MPC 回帰パラメータテーブルを管理する。
    /// </summary>
    public class MpcParameterTable
    {
        /// <summary>
        /// パラメータテーブル: [stage][cutPairIndex] -> MpcParameters
        /// </summary>
        private readonly Dictionary<int, Dictionary<int, MpcParameters>> _table;

        /// <summary>
        /// カットペア定義リスト
        /// </summary>
        public IReadOnlyList<MpcCutPair> CutPairs { get; }

        /// <summary>
        /// 信頼度に対応する z 値（Phi^{-1}(p)）。p = 0.95 の場合 1.645。
        /// </summary>
        public double ZValue { get; }

        /// <summary>
        /// コンストラクタ。デフォルトパラメータで初期化する。
        /// </summary>
        public MpcParameterTable()
        {
            CutPairs = new List<MpcCutPair>
            {
                new MpcCutPair(2, 6),
                new MpcCutPair(4, 10),
                new MpcCutPair(6, 14),
            };
            ZValue = 1.645; // p = 0.95
            _table = BuildDefaultTable();
        }

        /// <summary>
        /// 指定ステージ・カットペアの回帰パラメータを取得する。
        /// </summary>
        /// <param name="stage">ゲームステージ（1〜15）</param>
        /// <param name="cutPairIndex">カットペアのインデックス（0〜2）</param>
        /// <returns>回帰パラメータ。該当なしの場合は null</returns>
        public MpcParameters? GetParameters(int stage, int cutPairIndex)
        {
            if (_table.TryGetValue(stage, out var pairs) &&
                pairs.TryGetValue(cutPairIndex, out var parameters))
            {
                return parameters;
            }
            return null;
        }

        /// <summary>
        /// デフォルトのパラメータテーブルを構築する。
        /// ステージ区分（序盤/中盤/終盤）ごとに sigma 値を設定し、
        /// 全ステージ共通で a=1.0, b=0.0 とする。
        /// </summary>
        /// <returns>パラメータテーブル</returns>
        private Dictionary<int, Dictionary<int, MpcParameters>> BuildDefaultTable()
        {
            var table = new Dictionary<int, Dictionary<int, MpcParameters>>();

            // ステージ区分ごとの sigma 値: [cutPairIndex] -> sigma
            var sigmaByStageBand = new Dictionary<string, double[]>
            {
                { "early", new[] { 800.0, 1200.0, 1600.0 } },  // 序盤（ステージ 1〜5）
                { "mid",   new[] { 500.0,  800.0, 1100.0 } },  // 中盤（ステージ 6〜10）
                { "late",  new[] { 300.0,  500.0,  700.0 } },   // 終盤（ステージ 11〜15）
            };

            for (int stage = 1; stage <= 15; stage++)
            {
                // ステージ区分の判定
                double[] sigmas;
                if (stage <= 5)
                {
                    sigmas = sigmaByStageBand["early"];
                }
                else if (stage <= 10)
                {
                    sigmas = sigmaByStageBand["mid"];
                }
                else
                {
                    sigmas = sigmaByStageBand["late"];
                }

                var pairs = new Dictionary<int, MpcParameters>();
                for (int cutPairIndex = 0; cutPairIndex < 3; cutPairIndex++)
                {
                    pairs[cutPairIndex] = new MpcParameters(
                        a: 1.0,
                        b: 0.0,
                        sigma: sigmas[cutPairIndex]);
                }
                table[stage] = pairs;
            }

            return table;
        }
    }
}
