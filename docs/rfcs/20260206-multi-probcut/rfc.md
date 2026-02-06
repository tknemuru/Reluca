# [RFC] Multi-ProbCut による選択的探索の実装

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-06 |
| **タグ** | Search, 高優先度 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md), [WZebra アルゴリズム解析](../../reports/wzebra-algorithm.md), [RFC 1: nodes-searched-instrumentation](../20260205-nodes-searched-instrumentation/rfc.md) |

## 1. 要約 (Summary)

- PVS 探索エンジンに Multi-ProbCut (MPC) による選択的探索を導入し、探索深度の大幅な向上を実現する。
- 浅い探索の評価値と深い探索の評価値の間に存在する線形相関を利用し、統計的な信頼度に基づいて枝刈りを判定する。
- ゲームステージ（石数ベース）ごとに最適化された回帰パラメータ（$a, b, \sigma$）を管理し、局面の性質に応じた枝刈りの強度を制御する。
- 複数のカットペア（浅い探索深さ $d'$ と深い探索深さ $d$ の組み合わせ）を多段階に適用し、探索木の各階層で無駄な計算を削減する。
- `SearchOptions.UseMultiProbCut` による ON/OFF 切り替えを実装し、既存の探索動作との互換性を維持する。

## 2. 背景・動機 (Motivation)

- 現在の Reluca の PVS 探索エンジンは、Alpha-Beta 枝刈り・置換表・反復深化・Aspiration Window を実装済みであるが、探索深度は 14〜16 手程度が上限である。これは全幅探索のアプローチに起因する構造的な制約であり、Alpha-Beta の枝刈り効率をこれ以上向上させることは困難である。
- WZebra が中盤で深さ 18〜27 手に到達できる最大の要因は Multi-ProbCut (MPC) の実装にある。MPC は「浅い探索の結果から深い探索の結果を統計的に予測し、高い信頼度でカットオフが発生すると判断できる分岐の探索を省略する」選択的探索技術である。
- オセロは Null Move Pruning が適用不能なゲーム（Zugzwang が頻発するため）であり、MPC のような統計的枝刈りが探索深度を飛躍させるための唯一の有効手段である。
- MPC を導入しなければ、同一時間内での探索深度は現状のまま据え置きとなり、WZebra 水準の着手品質には到達できない。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- MPC ON 時に NodesSearched が MPC OFF 時と比較して有意に減少すること（目標: 50% 以上の削減）
- MPC ON 時の探索深さが MPC OFF 時より深くなること（同一時間内）
- MPC ON/OFF いずれでも致命的な着手品質の劣化がないこと（対 Legacy 勝率で検証）
- ProbCut の回帰モデル（$v_d \approx a \cdot v_{d'} + b + e$）を実装し、信頼度 $p$ に基づく枝刈り判定を行うこと
- ゲームステージごとに回帰パラメータを管理し、局面の性質に応じた枝刈り強度の制御を実現すること

### やらないこと (Non-Goals)

- 自己対戦データからの回帰パラメータの自動学習。本 RFC では WZebra 文献ベースの初期値を手動設定する
- 終盤特化型 MPC の実装。終盤の完全読み切りフェーズでの MPC 適用は将来 RFC のスコープとする
- NPS（Nodes Per Second）の計測。時間計測は RFC 4（time-limit-search）のスコープとする
- 並列探索との統合

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- RFC 1（nodes-searched-instrumentation）が完了していること。MPC の効果は NodesSearched の削減量で評価するため、計測基盤が必須である
- PVS 探索エンジン（`PvsSearchEngine`）が反復深化・TT・Aspiration Window を統合済みであること（実装済み）
- ゲームステージ（`Stage`）が石数ベースで 1〜15 に分割済みであること（実装済み: `Stage.Unit = 4`, `Stage.Max = 15`）
- 評価関数（`FeaturePatternEvaluator`）がステージごとの評価値テーブルを保持していること（実装済み: `Reluca/Resources/evaluated-value.{1-15}.txt`）

## 5. 詳細設計 (Detailed Design)

### 5.1 Multi-ProbCut の数理モデル

#### 5.1.1 線形回帰モデル

浅い探索深さ $d'$ の評価値 $v_{d'}$ と深い探索深さ $d$ の評価値 $v_d$ の間には、以下の線形回帰関係が成立する。

$$v_d \approx a \cdot v_{d'} + b + e$$

ここで:
- $a$: 回帰係数（傾き）
- $b$: 切片
- $e$: 誤差項（平均 0、標準偏差 $\sigma$ の正規分布に従うと仮定）

#### 5.1.2 枝刈り判定式

Alpha-Beta 探索において、ある局面の評価値が $\beta$ 以上であれば beta カットが成立する。MPC では、浅い探索の結果 $v_{d'}$ を用いて、深い探索の結果 $v_d$ が $\beta$ 以上になる確率が信頼度 $p$ 以上であるかを判定する。

**Beta カット判定（上限カット）:**

$$v_{d'} \ge \frac{\Phi^{-1}(p) \cdot \sigma + \beta - b}{a}$$

この条件が成立する場合、深い探索を行えば確率 $p$ 以上で beta カットが発生すると推定し、深い探索を省略する。

**Alpha カット判定（下限カット）:**

$$v_{d'} \le \frac{-\Phi^{-1}(p) \cdot \sigma + \alpha - b}{a}$$

この条件が成立する場合、深い探索を行えば確率 $p$ 以上で alpha カット（fail-low）が発生すると推定し、深い探索を省略する。

ここで $\Phi^{-1}(p)$ は標準正規分布の逆累積分布関数であり、$p = 0.95$ の場合 $\Phi^{-1}(0.95) \approx 1.645$ である。

### 5.2 カットペアの構成

MPC では、複数の $(d', d)$ ペアを定義し、多段階に枝刈りを適用する。本 RFC では以下の 3 組のカットペアを採用する。

| カットペア | 浅い探索深さ $d'$ | 深い探索深さ $d$ | 適用条件 |
|-----------|------------------|------------------|---------|
| Pair 1 | 2 | 6 | `remainingDepth >= 6` |
| Pair 2 | 4 | 10 | `remainingDepth >= 10` |
| Pair 3 | 6 | 14 | `remainingDepth >= 14` |

**[仮定]** カットペアの深さは、WZebra/Logistello の MPC 実装における多段階フィルタリングの構成に基づいている。WZebra アルゴリズム解析（[Section 4.4.1](../../reports/wzebra-algorithm.md)）によれば、WZebra は $(d'=4, d=8)$, $(d'=8, d=12)$, $(d'=12, d=16)$ のように深さ差 4 の等間隔ペアを連鎖的に適用する構成を採用している。本 RFC のカットペアは、Reluca の現状の探索深度上限（14〜16 手）に合わせてスケールダウンし、差分を $d - d' = 4, 6, 8$ と段階的に拡大する構成とした。差分が小さいペア（Pair 1: 差 4）は浅い探索と深い探索の相関が強く高い的中率が期待でき、差分が大きいペア（Pair 3: 差 8）はカットの効果が大きいが的中率がやや下がるトレードオフがある。Buro (1997) の Multi-ProbCut 論文においても、カットペアの差分は分岐係数と評価関数の安定性に依存するとされており、オセロの平均分岐係数（約 10）では差分 4〜8 が有効な範囲とされている。実装後のベンチマークで効果が不十分な場合、ペアの追加や深さの調整を行う。

#### 適用ロジック

探索ノードにおいて `remainingDepth` が各カットペアのターゲット深度 $d$ 以上である場合に、対応する浅い探索深さ $d'$ で MPC 判定を実行する。カットペアは深い方から順に判定し、最初にカット条件が成立した時点で枝刈りを確定する。

### 5.3 回帰パラメータの管理

#### 5.3.1 MpcParameters クラス

回帰パラメータを保持するデータクラスを新設する。

ファイル: `Reluca/Search/MpcParameters.cs`

```csharp
/// <summary>
/// Multi-ProbCut の回帰パラメータを保持する
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
```

#### 5.3.2 MpcCutPair クラス

カットペアの定義を保持するデータクラスを新設する。

ファイル: `Reluca/Search/MpcCutPair.cs`

```csharp
/// <summary>
/// Multi-ProbCut のカットペア定義を保持する
/// </summary>
public class MpcCutPair
{
    /// <summary>
    /// 浅い探索深さ
    /// </summary>
    public int ShallowDepth { get; }

    /// <summary>
    /// 深い探索深さ（適用条件: remainingDepth >= DeepDepth）
    /// </summary>
    public int DeepDepth { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="shallowDepth">浅い探索深さ</param>
    /// <param name="deepDepth">深い探索深さ</param>
    public MpcCutPair(int shallowDepth, int deepDepth)
    {
        ShallowDepth = shallowDepth;
        DeepDepth = deepDepth;
    }
}
```

#### 5.3.3 MpcParameterTable クラス

ステージごと・カットペアごとの回帰パラメータを管理するテーブルを新設する。

ファイル: `Reluca/Search/MpcParameterTable.cs`

```csharp
/// <summary>
/// ステージ別・カットペア別の MPC 回帰パラメータテーブルを管理する
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
    /// 信頼度に対応する z 値（Phi^{-1}(p)）
    /// </summary>
    public double ZValue { get; }

    /// <summary>
    /// コンストラクタ。デフォルトパラメータで初期化する
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
    /// 指定ステージ・カットペアの回帰パラメータを取得する
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
```

#### 5.3.4 初期パラメータの設定方針

**[仮定]** 回帰パラメータの初期値は、以下の方針で設定する。

1. **傾き $a$**: 全ステージで $a = 1.0$ を初期値とする。浅い探索と深い探索の評価値スケールが同一であるため、傾きは 1.0 に近いと仮定する。
2. **切片 $b$**: 全ステージで $b = 0.0$ を初期値とする。Reluca の評価関数はステージごとに独立した重みテーブルを使用するため、系統的なバイアスは小さいと仮定する。
3. **標準偏差 $\sigma$**: ステージに応じて変動させる。
   - 序盤（ステージ 1〜5）: $\sigma$ を大きめに設定（慎重な枝刈り）
   - 中盤（ステージ 6〜10）: $\sigma$ を中程度に設定
   - 終盤（ステージ 11〜15）: $\sigma$ を小さめに設定（積極的な枝刈り）
4. **カットペアによる $\sigma$ の変動**: 浅い探索と深い探索の深さの差が大きいほど予測精度が下がるため、Pair 3 > Pair 2 > Pair 1 の順で $\sigma$ を大きく設定する。

具体的な初期値は以下の通りである。$\sigma$ の単位は Reluca の評価値スケール（`FeaturePatternEvaluator` の出力値）に準じる。

| ステージ区分 | Pair 1 (d'=2, d=6) $\sigma$ | Pair 2 (d'=4, d=10) $\sigma$ | Pair 3 (d'=6, d=14) $\sigma$ |
|------------|----------------------------|------------------------------|------------------------------|
| 序盤 (1〜5) | 800 | 1200 | 1600 |
| 中盤 (6〜10) | 500 | 800 | 1100 |
| 終盤 (11〜15) | 300 | 500 | 700 |

**[仮定]** これらの $\sigma$ 値は仮設定であり、実装後に自己対戦データから実測値を取得して調整する。$\sigma$ が小さすぎると誤った枝刈りが増加し着手品質が劣化し、大きすぎると枝刈りが発生しにくくなり探索効率の改善が不十分となる。初期値は保守的（大きめ）に設定し、品質検証を通じて段階的に最適化する方針とする。$\sigma$ 調整の具体的な判定基準は Section 8 の「$\sigma$ パラメータの調整基準」に定義する。

### 5.4 PvsSearchEngine への MPC 統合

#### 5.4.1 Pvs() メソッドへの MPC 判定の挿入

MPC の判定は `Pvs()` メソッドにおいて、TT Probe の後、合法手の展開の前に挿入する。

```csharp
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
{
    _nodesSearched++;

    // 終了条件
    if (remainingDepth == 0 || BoardAccessor.IsGameEndTurnCount(context))
    {
        return Evaluate(context);
    }

    long originalAlpha = alpha;

    // TT Probe
    ulong hash = 0;
    if (_options?.UseTranspositionTable == true)
    {
        hash = _zobristHash.ComputeHash(context);
        if (_transpositionTable.TryProbe(hash, remainingDepth, alpha, beta, out var entry))
        {
            return entry.Value;
        }
    }

    // === MPC 判定（ここに挿入） ===
    if (_mpcEnabled && !isPassed)
    {
        var mpcResult = TryMultiProbCut(context, remainingDepth, alpha, beta);
        if (mpcResult.HasValue)
        {
            return mpcResult.Value;
        }
    }

    // 合法手展開（既存処理を維持）
    // ...
}
```

**[仮定]** パス直後のノード（`isPassed == true`）では MPC を適用しない。パス後は手番が切り替わった直後であり、局面の性質が大きく変化している可能性があるため、浅い探索の予測精度が低下するリスクがある。

#### 5.4.2 TryMultiProbCut メソッド

MPC の判定を行うメソッドを `PvsSearchEngine` に追加する。

```csharp
/// <summary>
/// Multi-ProbCut による枝刈り判定を行う。
/// カットペアを深い方から順に評価し、カット条件が成立した場合はカット値を返す。
/// カット条件が成立しない場合は null を返す。
/// </summary>
/// <param name="context">現在のゲーム状態</param>
/// <param name="remainingDepth">残り探索深さ</param>
/// <param name="alpha">アルファ値</param>
/// <param name="beta">ベータ値</param>
/// <returns>カット値。カットしない場合は null</returns>
private long? TryMultiProbCut(GameContext context, int remainingDepth, long alpha, long beta)
{
    var cutPairs = _mpcParameterTable.CutPairs;
    double zValue = _mpcParameterTable.ZValue;

    // カットペアを深い方から順に判定
    for (int i = cutPairs.Count - 1; i >= 0; i--)
    {
        var pair = cutPairs[i];

        // 適用条件: remainingDepth >= deepDepth
        if (remainingDepth < pair.DeepDepth)
        {
            continue;
        }

        var parameters = _mpcParameterTable.GetParameters(context.Stage, i);
        if (parameters == null)
        {
            continue;
        }

        double a = parameters.A;
        double b = parameters.B;
        double sigma = parameters.Sigma;

        // 浅い探索を実行（MPC 用の探索では MPC を再帰適用しない）
        // Aspiration retry 中の _suppressTTStore も一時的に解除する
        // （MPC 用浅い探索は独立した探索であり、Aspiration retry の TT Store 抑制の影響を受けるべきではない）
        bool savedMpcFlag = _mpcEnabled;
        bool savedSuppressTTStore = _suppressTTStore;
        _mpcEnabled = false;
        _suppressTTStore = false;
        long shallowValue = Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false);
        _mpcEnabled = savedMpcFlag;
        _suppressTTStore = savedSuppressTTStore;

        // Beta カット判定: shallowValue >= (zValue * sigma + beta - b) / a
        double betaThreshold = (zValue * sigma + (double)beta - b) / a;
        if (shallowValue >= (long)Math.Ceiling(betaThreshold))
        {
            _mpcCutCount++;
            return beta; // beta カット
        }

        // Alpha カット判定: shallowValue <= (-zValue * sigma + alpha - b) / a
        double alphaThreshold = (-zValue * sigma + (double)alpha - b) / a;
        if (shallowValue <= (long)Math.Floor(alphaThreshold))
        {
            _mpcCutCount++;
            return alpha; // alpha カット（fail-low）
        }
    }

    return null; // カットなし
}
```

#### 5.4.3 MPC 用フラグの管理

MPC の浅い探索内で再帰的に MPC が適用されることを防止するため、インスタンスフラグ `_mpcEnabled` を導入する。

```csharp
/// <summary>
/// MPC の再帰適用を防止するフラグ
/// </summary>
private bool _mpcEnabled;
```

`Search()` メソッドの冒頭で `_mpcEnabled = options.UseMultiProbCut` に初期化する。`TryMultiProbCut()` 内で浅い探索を実行する際に `_mpcEnabled = false` に設定し、探索完了後に元の値を復元する。

`Pvs()` メソッドの MPC 判定条件は `_mpcEnabled` を使用する（`_options?.UseMultiProbCut == true` ではなく `_mpcEnabled` で判定することで、再帰適用を防止する）。

```csharp
// MPC 判定（_mpcEnabled で再帰適用を防止）
if (_mpcEnabled && !isPassed)
{
    var mpcResult = TryMultiProbCut(context, remainingDepth, alpha, beta);
    if (mpcResult.HasValue)
    {
        return mpcResult.Value;
    }
}
```

**注記（スレッドセーフティ）:** 本設計はシングルスレッド前提である。`_mpcEnabled` および `_suppressTTStore` の save/restore パターンはスレッドセーフではない。将来、並列探索を導入する際には、これらのフラグ管理方式をスレッドローカル変数または引数渡しに変更する必要がある。

#### 5.4.4 MPC カットカウンタの管理

MPC カットの発生回数を計測するため、インスタンスフィールド `_mpcCutCount` を `PvsSearchEngine` に追加する。

```csharp
/// <summary>
/// MPC カット発生回数カウンタ
/// </summary>
private long _mpcCutCount;
```

- **リセットタイミング**: `Search()` メソッドの反復深化ループ内で、各深さの探索開始時に `_nodesSearched` と同様にリセットする（`_mpcCutCount = 0`）。
- **インクリメント位置**: `TryMultiProbCut()` 内で beta カットまたは alpha カットが成立し、値を `return` する直前にインクリメントする。
- **ログ出力**: 反復深化の各深さ完了時のログに `MpcCuts` として出力する（Section 7.2 参照）。

```csharp
// Search() メソッド内の反復深化ループ
for (int depth = 1; depth <= options.MaxDepth; depth++)
{
    _currentDepth = depth;
    _nodesSearched = 0;
    _mpcCutCount = 0;  // 各反復の開始時にリセット

    // ... 探索実行 ...

    _logger.LogInformation("探索進捗 {@SearchProgress}", new
    {
        Depth = depth,
        Nodes = _nodesSearched,
        TotalNodes = totalNodesSearched,
        Value = result.Value,
        MpcCuts = _mpcCutCount
    });
}
```

#### 5.4.5 浅い探索の窓幅

`TryMultiProbCut()` 内の浅い探索ではフルウィンドウ（`DefaultAlpha`, `DefaultBeta`）を使用する。

**[仮定]** 浅い探索の正確性を優先するためフルウィンドウを採用する。Null Window を使用して高速化する手法も存在するが、MPC の判定精度に直接影響するため、初期実装ではフルウィンドウとする。パフォーマンスが不十分な場合、Null Window 方式への変更を検討する。

### 5.5 SearchOptions への MPC パラメータ追加

`Reluca/Search/SearchOptions.cs` に MPC の ON/OFF フラグを追加する。

```csharp
public class SearchOptions
{
    // ... 既存プロパティ ...

    /// <summary>
    /// Multi-ProbCut を使用するかどうか
    /// </summary>
    public bool UseMultiProbCut { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SearchOptions(
        int maxDepth = DefaultMaxDepth,
        bool useTranspositionTable = false,
        bool useAspirationWindow = false,
        long aspirationDelta = DefaultAspirationDelta,
        int aspirationMaxRetry = DefaultAspirationMaxRetry,
        bool useMultiProbCut = false)
    {
        MaxDepth = maxDepth;
        UseTranspositionTable = useTranspositionTable;
        UseAspirationWindow = useAspirationWindow;
        AspirationDelta = aspirationDelta;
        AspirationMaxRetry = aspirationMaxRetry;
        UseMultiProbCut = useMultiProbCut;
    }
}
```

### 5.6 MpcParameterTable の DI 登録

`MpcParameterTable` は Singleton として `DiProvider` に登録する。探索エンジンのインスタンス間で共有可能であり、パラメータは不変である。

`Reluca/Di/DiProvider.cs` に追加:

```csharp
services.AddSingleton<MpcParameterTable>();
```

`PvsSearchEngine` のコンストラクタに `MpcParameterTable` を DI で注入する。

```csharp
public PvsSearchEngine(
    ILogger<PvsSearchEngine> logger,
    MobilityAnalyzer mobilityAnalyzer,
    MoveAndReverseUpdater reverseUpdater,
    ITranspositionTable transpositionTable,
    IZobristHash zobristHash,
    MpcParameterTable mpcParameterTable)
{
    // ... 既存の初期化 ...
    _mpcParameterTable = mpcParameterTable;
}
```

### 5.7 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Reluca/Search/MpcParameters.cs` | **新規作成** - 回帰パラメータのデータクラス |
| `Reluca/Search/MpcCutPair.cs` | **新規作成** - カットペア定義のデータクラス |
| `Reluca/Search/MpcParameterTable.cs` | **新規作成** - ステージ別パラメータテーブルの管理 |
| `Reluca/Search/PvsSearchEngine.cs` | MPC 判定ロジックの追加、`TryMultiProbCut()` メソッド追加、`_mpcEnabled` フラグ追加、`_mpcCutCount` カウンタ追加 |
| `Reluca/Search/SearchOptions.cs` | `UseMultiProbCut` プロパティ追加 |
| `Reluca/Di/DiProvider.cs` | `MpcParameterTable` の DI 登録 |

## 6. 代替案の検討 (Alternatives Considered)

### 回帰パラメータの決定方法

#### 案A: WZebra 文献ベースの初期値設定（採用案）

- **概要**: WZebra/Logistello の論文で報告されているパラメータ特性（$a \approx 1.0$, $b \approx 0$, $\sigma$ はステージ依存）に基づき、手動で初期値を設定する。実装後にベンチマークで調整する。
- **長所**: 実装コストが低く、MPC の効果を早期に検証できる。パラメータ調整は独立した作業として段階的に実施可能。
- **短所**: 初期値が最適でない可能性があり、想定通りの枝刈り効果が得られないリスクがある。

#### 案B: 自己対戦データからの回帰分析

- **概要**: Reluca の自己対戦を大量に実行し、各局面について浅い探索と深い探索の評価値ペアを収集する。収集したデータに対して最小二乗法で回帰分析を行い、ステージ別・カットペア別の $(a, b, \sigma)$ を算出する。
- **長所**: Reluca 固有の評価関数に最適化されたパラメータが得られる。統計的に裏付けのある値であるため、枝刈りの信頼性が高い。
- **短所**: 自己対戦データの生成に大量の計算時間を要する（数千〜数万局）。データ収集ツールの実装が必要であり、MPC の効果検証が大幅に遅延する。評価関数を変更するたびにパラメータの再計算が必要になる。

#### 選定理由

MPC の効果を早期に検証するため、案 A を採用する。案 B は統計的精度で優れるが、データ収集と分析のコストが高く、WZebra ロードマップのクリティカルパス上にある本 RFC の完了を遅延させるリスクがある。案 A の初期パラメータで MPC の基本的な動作を確認した後、パラメータ最適化は独立した改善作業として実施すれば十分である。

### MPC 判定の挿入位置

#### 案C: TT Probe 後・合法手展開前（採用案）

- **概要**: `Pvs()` メソッドにおいて TT Probe の後、合法手の展開・ソートの前に MPC 判定を行う。
- **長所**: TT にヒットすれば MPC 判定を行う必要がなく、無駄な浅い探索を回避できる。合法手の展開コスト（`MobilityAnalyzer.Analyze`）を MPC カット時にスキップできる。
- **短所**: なし。

#### 案D: 合法手展開後・子ノード探索前

- **概要**: 合法手を展開した後、最初の子ノード探索の前に MPC 判定を行う。
- **長所**: 合法手の数に基づいて MPC の適用可否を判断できる（例: 合法手が少ない場合は MPC を抑制）。
- **短所**: MPC カット時にも合法手の展開コストが発生する。MPC の目的は「不要な探索を省略すること」であり、合法手の展開自体が無駄になる。

#### 選定理由

案 C を採用する。MPC カット時に合法手展開のコストを回避できるのは、特に深い探索ノードで顕著な利点となる。TT Probe 後に MPC 判定を置くことで、TT ヒット → MPC カット → 通常探索 という段階的なフィルタリングが実現され、各段階でのコスト削減が最大化される。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

- MPC 判定自体のコストは「浅い探索 1 回」である。この浅い探索のコストが、カットによって省略される深い探索のコストを下回る場合にのみ MPC は効果を発揮する。探索木の上位ノード（`remainingDepth` が大きいノード）でカットが発生するほど、省略される探索量が指数関数的に増大するため、カットペアの設計は深いノードを優先している。
- `TryMultiProbCut()` 内の浅い探索は TT を活用する。反復深化により浅い深さの探索結果は TT に蓄積されているため、MPC 用の浅い探索は TT ヒットにより高速に完了する可能性が高い。
- 閾値の計算（`betaThreshold`, `alphaThreshold`）は浮動小数点演算を含むが、1 ノードあたり最大 3 回（カットペア数）の計算であり、探索全体のコストに対する影響は無視できる。
- **フルウィンドウ浅い探索の最悪ケースにおけるノード数見積もり**: MPC 用の浅い探索はフルウィンドウで実行されるが、TT ヒットがない最悪ケースでも、各カットペアのノード数は以下の通り十分に小さい。オセロの平均分岐係数を 10、Alpha-Beta の理想的枝刈りでノード数が $b^{d'/2}$ に削減されると仮定すると、Pair 1 ($d'=2$): 最大約 $10^1 = 10$ ノード、Pair 2 ($d'=4$): 最大約 $10^2 = 100$ ノード、Pair 3 ($d'=6$): 最大約 $10^3 = 1{,}000$ ノードとなる。一方、カットにより省略される深い探索のノード数は、Pair 1 ($d=6$): 最大約 $10^3 = 1{,}000$ ノード、Pair 2 ($d=10$): 最大約 $10^5 = 100{,}000$ ノード、Pair 3 ($d=14$): 最大約 $10^7 = 10{,}000{,}000$ ノードである。各ペアにおいて浅い探索のコストはカット効果の $1/100$ 以下であり、MPC が効果を発揮する十分な余裕がある。

### 7.2 可観測性 (Observability)

- 構造化ログ基盤（Serilog）が導入済みであるため、MPC カットの発生回数をログ出力する。
- 反復深化の各深さ完了時のログに、MPC カット発生回数を追加する。

```csharp
_logger.LogInformation("探索進捗 {@SearchProgress}", new
{
    Depth = depth,
    Nodes = _nodesSearched,
    TotalNodes = totalNodesSearched,
    Value = result.Value,
    MpcCuts = _mpcCutCount  // MPC カット回数を追加
});
```

### 7.3 マイグレーションと後方互換性

- `SearchOptions` のコンストラクタにデフォルト値（`useMultiProbCut = false`）を設定するため、既存の呼び出し元は変更不要である。
- MPC OFF 時は既存の探索動作と完全に同一である。
- `ISearchEngine` インターフェースの変更はない。
- 破壊的変更はない。

## 8. テスト戦略 (Test Strategy)

### ユニットテスト

| テスト観点 | 内容 | テスト種別 |
|-----------|------|-----------|
| MPC OFF 時の非干渉 | MPC OFF 時の `BestMove` と `Value` が MPC 追加前と一致すること | ユニット |
| MPC ON 時の NodesSearched 削減 | 同一局面で MPC ON 時の NodesSearched が MPC OFF 時より少ないこと | ユニット |
| カット判定の正確性 | 既知の回帰パラメータと閾値で、カット条件の成立/不成立が正しく判定されること | ユニット |
| MpcParameterTable の取得 | 各ステージ・カットペアでパラメータが正しく取得できること | ユニット |
| MPC 再帰防止 | MPC の浅い探索内で MPC が再帰適用されないこと | ユニット |

### 統合テスト

| テスト観点 | 内容 |
|-----------|------|
| 着手品質の検証 | 複数の定石局面で MPC ON/OFF の着手を比較し、致命的な劣化がないこと |
| 対 Legacy 勝率 | MPC ON の PvsSearchEngine vs LegacySearchEngine で勝率が同等以上であること |

### $\sigma$ パラメータの調整基準

初期 $\sigma$ 値は仮設定であるため、フェーズ 4（品質検証とパラメータ調整）において以下の定量的基準に従い調整を行う。

| 指標 | 閾値 | アクション |
|------|------|-----------|
| NodesSearched 削減率 | 30% 未満 | 全ステージの $\sigma$ を一律 20% 削減する（枝刈りを積極化） |
| NodesSearched 削減率 | 50% 以上 | 目標達成。$\sigma$ は現状維持とする |
| 対 Legacy 勝率の低下 | 5% 以上低下 | 全ステージの $\sigma$ を一律 20% 増加する（枝刈りを慎重化） |
| 対 Legacy 勝率の低下 | 10% 以上低下 | MPC を OFF にして原因調査を行う。$\sigma$ を初期値の 1.5 倍に設定し再検証する |

調整は 1 パラメータずつ段階的に実施し、各調整後に NodesSearched 削減率と対 Legacy 勝率の両指標を再計測する。両指標が「NodesSearched 削減率 30% 以上」かつ「対 Legacy 勝率低下 5% 未満」を同時に満たす状態を収束条件とする。

### 検証方法

- 既存テストを全て実行し、MPC OFF 時に探索結果が変更されないことを確認する
- テスト用の固定局面（序盤・中盤・終盤）で MPC ON/OFF の NodesSearched を取得し、MPC ON 時に削減されていることを検証する
- 対 Legacy 自動対局を実施し、勝率に有意な劣化がないことを確認する

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: データモデルの実装

- `MpcParameters`, `MpcCutPair`, `MpcParameterTable` の 3 クラスを新規作成する
- `MpcParameterTable` に初期パラメータ（文献ベース）を設定する
- `MpcParameterTable` の DI 登録を `DiProvider` に追加する
- ユニットテスト: パラメータ取得の検証

### フェーズ 2: SearchOptions の拡張

- `SearchOptions` に `UseMultiProbCut` プロパティを追加する
- 既存テストが全て通過することを確認する（後方互換性の検証）

### フェーズ 3: PvsSearchEngine への MPC 統合

- `PvsSearchEngine` に `_mpcEnabled` フラグと `_mpcCutCount` カウンタを追加する
- `Pvs()` メソッドに MPC 判定の挿入ポイントを追加する
- `TryMultiProbCut()` メソッドを実装する（`_suppressTTStore` の退避・復元を含む）
- `Search()` メソッドでの初期化とログ出力を追加する
- ユニットテスト: MPC OFF 時の非干渉、MPC ON 時の NodesSearched 削減

### フェーズ 4: 品質検証とパラメータ調整

- 複数の局面で MPC ON/OFF の NodesSearched を比較し、削減率を計測する
- 対 Legacy 自動対局を実施し、勝率を確認する
- Section 8 の「$\sigma$ パラメータの調整基準」に従い、$\sigma$ パラメータを調整する
- 必要に応じてカットペアの追加・変更を検討する

### リスク軽減策

- `UseMultiProbCut = false` がデフォルトであるため、MPC は明示的に有効化しない限り動作しない
- パラメータ調整は `MpcParameterTable` のみの変更で完結し、探索ロジックへの影響はない

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響あり。Search コンポーネントに `MpcParameterTable` が追加されるため、主要コンポーネント一覧を更新する必要がある。実装完了時に更新を行う。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータモデルに変更はない。MPC は探索アルゴリズムの内部最適化であり、ドメイン層には影響しない。
- `docs/api-overview.md`: 存在しない（対象外）。
