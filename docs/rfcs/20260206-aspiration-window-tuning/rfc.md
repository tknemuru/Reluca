# [RFC] Aspiration Window パラメータ最適化

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-06 |
| **タグ** | Search, 中優先度 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md), [WZebra アルゴリズム解析](../../reports/wzebra-algorithm.md), [RFC 1: nodes-searched-instrumentation](../20260205-nodes-searched-instrumentation/rfc.md), [RFC 2: multi-probcut](../20260206-multi-probcut/rfc.md) |

## 1. 要約 (Summary)

- PVS 探索エンジンの Aspiration Window パラメータを最適化し、探索効率（NodesSearched）を改善する。
- 現在の固定幅拡張戦略（delta を 2 倍に拡大）を指数拡張戦略に変更し、fail-low/fail-high 時の再探索回数を削減する。
- delta 初期値（現在 50）をゲームステージおよび探索深さに応じて動的に設定する仕組みを導入する。
- 最大リトライ回数（現在 3）の妥当性を定量的に検証し、必要に応じて調整する。
- retry 発生率と探索効率のトレードオフを定量的に整理し、パラメータ選定の根拠を示す。

## 2. 背景・動機 (Motivation)

- Aspiration Window は反復深化と組み合わせて使用される探索窓の最適化技術であり、前回反復の評価値を中心に狭い窓で探索を開始し、窓内に収まれば効率的にカットオフを実現する。Task 3d で基本実装は完了しており、ON/OFF 一致も確認済みである。
- 現在の実装は以下のデフォルト値をハードコーディングしている。
  - `AspirationDelta = 50`: 初期ウィンドウ幅
  - `AspirationMaxRetry = 3`: 最大再探索回数
  - 拡張戦略: `delta *= 2`（固定倍率）
- これらの値は暫定的に設定されたものであり、Reluca の評価関数（`FeaturePatternEvaluator`）の出力スケールや、ゲームステージごとの評価値の変動特性に基づいた最適化は行われていない。
- 不適切な delta は以下の問題を引き起こす。
  - **delta が小さすぎる場合**: fail-low/fail-high が頻発し、再探索回数が増加する。各再探索では TT Store が抑制されるため、TT ヒット率も低下する。最悪ケースではフルウィンドウへのフォールバックが発生し、Aspiration Window の恩恵を全く受けられない。
  - **delta が大きすぎる場合**: 窓が広すぎて Alpha-Beta のカットオフ効率が低下し、フルウィンドウ探索と大差なくなる。Aspiration Window を使用するオーバーヘッド（成功時の再探索）のみが残る。
- RFC 1（nodes-searched-instrumentation）の完了により NodesSearched の計測が可能となったため、パラメータ変更の効果を定量的に評価できる状態にある。
- RFC 2（multi-probcut）の MPC と Aspiration Window の相互作用も考慮する必要がある。MPC により評価値の安定性が変化する可能性があるため、MPC ON/OFF 両方の条件下でパラメータを検証する。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- チューニング後の NodesSearched がチューニング前と比較して改善すること
- retry 発生率と探索効率のトレードオフが定量的に整理されていること
- 既存テストが全て通過すること
- ステージ別・深さ別の delta 動的設定により、探索のあらゆる局面で適切な窓幅が選択されること
- 拡張戦略の改善により、fail-low/fail-high 発生時のフルウィンドウ到達率が低減すること

### やらないこと (Non-Goals)

- Aspiration Window の ON/OFF ロジックそのものの変更。基本的な再探索フレームワークは既存実装を維持する
- 評価関数の変更。delta の最適化は現在の `FeaturePatternEvaluator` の出力スケールを前提とする
- 並列探索との統合
- 自動パラメータチューニング（機械学習ベース）の導入。本 RFC では手動調整とベンチマーク検証に限定する

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- RFC 1（nodes-searched-instrumentation）が完了していること。パラメータ変更の効果は NodesSearched で計測するため、計測基盤が必須である
- 現在の Aspiration Window 実装（Task 3d）が正常に動作していること（実装済み・検証済み）
- PVS 探索エンジン（`PvsSearchEngine`）が反復深化・TT・Aspiration Window を統合済みであること（実装済み）
- ゲームステージ（`Stage`）が石数ベースで 1〜15 に分割済みであること（実装済み: `Stage.Unit = 4`, `Stage.Max = 15`）

## 5. 詳細設計 (Detailed Design)

### 5.1 現状分析

#### 5.1.1 現在の Aspiration Window 実装

`PvsSearchEngine.AspirationRootSearch()` は以下のフローで動作する。

```
1. delta = AspirationDelta (固定値 50)
2. while (retryCount <= AspirationMaxRetry):
   a. initialAlpha = prevValue - delta
   b. initialBeta  = prevValue + delta
   c. _suppressTTStore = true
   d. RootSearch(context, depth, alpha, beta)
   e. if fail-low:  delta *= 2, retryCount++
   f. if fail-high: delta *= 2, retryCount++
   g. if 窓内:     _suppressTTStore = false → 再探索して return
3. フォールバック: フルウィンドウで探索
```

#### 5.1.2 現在のパラメータ定義

`SearchOptions.cs` の定数:

```csharp
private const long DefaultAspirationDelta = 50;
private const int DefaultAspirationMaxRetry = 3;
```

#### 5.1.3 課題

1. **delta 初期値が固定**: Reluca の評価関数の出力スケールは数百〜数万の範囲を取り、ステージや探索深さによって変動幅が異なる。delta = 50 が全局面で適切かは未検証である。
2. **拡張倍率が固定 2 倍**: fail-low/fail-high 発生時に delta を 2 倍にするだけの単純な戦略である。1 回目の fail に対して 2 倍で十分な場合と不十分な場合が混在し、不必要に広い窓や不十分な拡張が発生しうる。
3. **最大リトライ回数 3 の根拠なし**: 3 回で十分かどうかの検証がなされていない。
4. **ステージ無関係**: 序盤の評価値は不安定（変動大）、終盤は安定（変動小）であるが、同一の delta を使用している。

### 5.2 delta 初期値の最適化

#### 5.2.1 ステージ別 delta テーブルの導入

評価値の変動特性はステージによって異なるため、ステージ別の delta 初期値テーブルを `AspirationParameterTable` クラスとして導入する。

ファイル: `Reluca/Search/AspirationParameterTable.cs`

```csharp
/// <summary>
/// Aspiration Window のステージ別パラメータテーブルを管理する。
/// </summary>
public class AspirationParameterTable
{
    /// <summary>
    /// ステージ別の delta 初期値テーブル
    /// </summary>
    private readonly Dictionary<int, long> _deltaByStage;

    /// <summary>
    /// デフォルトの delta 初期値（テーブルにないステージ用）
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
        return _deltaByStage.TryGetValue(stage, out var delta) ? delta : DefaultDelta;
    }

    /// <summary>
    /// デフォルトのステージ別 delta テーブルを構築する。
    /// </summary>
    /// <returns>ステージ別 delta テーブル</returns>
    private Dictionary<int, long> BuildDefaultTable()
    {
        var table = new Dictionary<int, long>();

        for (int stage = 1; stage <= 15; stage++)
        {
            if (stage <= 5)
            {
                // 序盤: 評価値の変動が大きい → delta を大きめに設定
                table[stage] = 80;
            }
            else if (stage <= 10)
            {
                // 中盤: 評価値が安定してくる → 標準的な delta
                table[stage] = 50;
            }
            else
            {
                // 終盤: 評価値が安定 → delta を小さめに設定
                table[stage] = 30;
            }
        }

        return table;
    }
}
```

**[仮定]** 序盤（ステージ 1〜5）で delta = 80、中盤（ステージ 6〜10）で delta = 50、終盤（ステージ 11〜15）で delta = 30 を初期値とする。これらの値は Reluca の評価関数の出力スケールに基づく仮設定であり、フェーズ 3（ベンチマーク検証）の結果に基づいて調整する。序盤の評価値は不確定要素が多く変動が大きいため大きな delta で fail を防ぎ、終盤は評価値が安定するため小さな delta で効率的なカットオフを狙う。

#### 5.2.2 深さ別 delta 補正

反復深化の浅い深さ（depth 2〜4）では評価値の変動が大きいため、ステージ別 delta に対して深さ補正係数を適用する。

```csharp
/// <summary>
/// 深さ補正係数を取得する。
/// 浅い深さでは delta を大きくし、深い深さでは縮小する。
/// </summary>
/// <param name="depth">探索深さ</param>
/// <returns>補正係数（1.0 以上）</returns>
public static double GetDepthFactor(int depth)
{
    if (depth <= 2) return 2.0;
    if (depth <= 4) return 1.5;
    return 1.0;
}
```

補正後の delta は `(long)(baseDelta * depthFactor)` として計算する。

**[仮定]** depth <= 2 で 2.0 倍、depth <= 4 で 1.5 倍、depth > 4 で 1.0 倍（補正なし）を初期値とする。浅い深さでは反復間の評価値の振れ幅が大きく、delta を大きくしないと不要な再探索が頻発する。深い深さでは前回反復からの評価値の変化が小さいため補正は不要である。

### 5.3 拡張戦略の改善

#### 5.3.1 指数拡張戦略の導入

現在の `delta *= 2` を指数拡張（`delta = delta * expansionFactor` で `expansionFactor` が回ごとに増大）に変更する。

```
retry 0: delta * 2     (現在と同じ)
retry 1: delta * 4     (2回連続 fail → より大きく拡張)
retry 2: delta * 8     (3回連続 fail → さらに大きく拡張)
```

具体的には、`AspirationRootSearch()` の拡張ロジックを以下のように変更する。

```csharp
private SearchResult AspirationRootSearch(GameContext context, int depth, long prevValue)
{
    // ステージ別・深さ別の delta 初期値を取得
    long baseDelta = _aspirationParameterTable.GetDelta(context.Stage);
    double depthFactor = AspirationParameterTable.GetDepthFactor(depth);
    long delta = (long)(baseDelta * depthFactor);
    int retryCount = 0;

    while (retryCount <= _options!.AspirationMaxRetry)
    {
        long initialAlpha = prevValue - delta;
        long initialBeta = prevValue + delta;

        long alpha = Math.Max(initialAlpha, DefaultAlpha);
        long beta = Math.Min(initialBeta, DefaultBeta);

        _suppressTTStore = true;
        var result = RootSearch(context, depth, alpha, beta);

        if (result.Value <= initialAlpha)
        {
            // fail-low: 指数拡張（2^(retryCount+1) 倍）
            delta = Math.Min(delta * (1L << (retryCount + 1)), MaxDelta);
            retryCount++;
        }
        else if (result.Value >= initialBeta)
        {
            // fail-high: 指数拡張（2^(retryCount+1) 倍）
            delta = Math.Min(delta * (1L << (retryCount + 1)), MaxDelta);
            retryCount++;
        }
        else
        {
            _suppressTTStore = false;
            return RootSearch(context, depth, alpha, beta);
        }
    }

    _suppressTTStore = false;
    return RootSearch(context, depth, DefaultAlpha, DefaultBeta);
}
```

#### 5.3.2 拡張戦略の比較

| 戦略 | retry 0 の拡張 | retry 1 の拡張 | retry 2 の拡張 | 特徴 |
|------|---------------|---------------|---------------|------|
| 現在（固定 2 倍） | ×2 | ×2 | ×2 | 拡張が控えめ。3 回 fail すると累積 8 倍 |
| 指数拡張（採用案） | ×2 | ×4 | ×8 | 後半で急激に拡張。累積 64 倍。フルウィンドウ到達を加速 |

指数拡張の狙いは、繰り返し fail する局面では早期にフルウィンドウに近い幅に拡張し、無駄な中間 retry を削減することである。1 回目の fail は delta が若干不足している可能性が高いため 2 倍で十分であるが、2 回目以降の fail は評価値が大きく変動している可能性が高く、より積極的な拡張が必要である。

### 5.4 最大リトライ回数の検討

#### 5.4.1 リトライ回数のコスト分析

各 retry は以下のコストを伴う。

1. `_suppressTTStore = true` での探索 1 回（TT Store が抑制される）
2. 成功時は `_suppressTTStore = false` での再探索 1 回

したがって、成功するまでに `retryCount + 1` 回の探索が発生する。フォールバック（フルウィンドウ）の場合は `MaxRetry + 2` 回の探索が発生する。

| MaxRetry | 成功時の最大探索回数 | フォールバック時の探索回数 |
|----------|---------------------|--------------------------|
| 2 | 6 (retry 2 回 + 確認 1 回) | 5 (retry 3 回 + FW 1 回) |
| 3 (現在) | 8 (retry 3 回 + 確認 1 回) | 6 (retry 4 回 + FW 1 回) |
| 4 | 10 (retry 4 回 + 確認 1 回) | 7 (retry 5 回 + FW 1 回) |

**[仮定]** 指数拡張戦略により、retry 2 回目で delta が初期値の 8 倍に達する。delta の初期値が適切に設定されていれば、3 回の retry で窓幅は十分に拡張され、4 回目以降の retry が必要になるケースは稀であると仮定する。ただし、この仮定はフェーズ 3 のベンチマークで retry 回数の分布を計測して検証する。

#### 5.4.2 MaxRetry の暫定値

MaxRetry = 3 を維持する。指数拡張戦略との組み合わせにより、3 回の retry で delta が初期値の 64 倍に拡張されるため、フルウィンドウ到達の可能性が十分に高い。フェーズ 3 のベンチマーク結果に基づき、フォールバック率が 5% を超える場合は MaxRetry の増加を検討する。

### 5.5 Aspiration Window の retry 計測機能

パラメータチューニングの判断材料として、retry の発生状況を計測・ログ出力する機能を追加する。

```csharp
// PvsSearchEngine に追加するフィールド
/// <summary>
/// Aspiration retry 発生回数カウンタ（深さごと）
/// </summary>
private int _aspirationRetryCount;

/// <summary>
/// Aspiration フルウィンドウフォールバック発生回数カウンタ（深さごと）
/// </summary>
private int _aspirationFallbackCount;
```

`AspirationRootSearch()` 内で retry 発生時に `_aspirationRetryCount` をインクリメントし、フォールバック発生時に `_aspirationFallbackCount` をインクリメントする。

反復深化のログ出力を拡張する。

```csharp
_logger.LogInformation("探索進捗 {@SearchProgress}", new
{
    Depth = depth,
    Nodes = _nodesSearched,
    TotalNodes = totalNodesSearched,
    Value = result.Value,
    MpcCuts = _mpcCutCount,
    AspirationRetries = _aspirationRetryCount,
    AspirationFallbacks = _aspirationFallbackCount
});
```

### 5.6 SearchOptions の変更

`SearchOptions.cs` を以下のように変更する。`AspirationDelta` プロパティは残すが、ステージ別テーブルが優先される旨をドキュメントに記載する。また、`AspirationUseStageTable` フラグを追加し、ステージ別テーブルの使用を ON/OFF 可能にする。

```csharp
public class SearchOptions
{
    // ... 既存定数 ...

    /// <summary>
    /// Aspiration Window のステージ別 delta テーブルを使用するかどうか。
    /// true の場合、AspirationDelta は無視され、ステージ別テーブルの値が使用される。
    /// </summary>
    public bool AspirationUseStageTable { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SearchOptions(
        int maxDepth = DefaultMaxDepth,
        bool useTranspositionTable = false,
        bool useAspirationWindow = false,
        long aspirationDelta = DefaultAspirationDelta,
        int aspirationMaxRetry = DefaultAspirationMaxRetry,
        bool aspirationUseStageTable = false,
        bool useMultiProbCut = false)
    {
        MaxDepth = maxDepth;
        UseTranspositionTable = useTranspositionTable;
        UseAspirationWindow = useAspirationWindow;
        AspirationDelta = aspirationDelta;
        AspirationMaxRetry = aspirationMaxRetry;
        AspirationUseStageTable = aspirationUseStageTable;
        UseMultiProbCut = useMultiProbCut;
    }
}
```

### 5.7 PvsSearchEngine の変更

`AspirationRootSearch()` の delta 取得ロジックを以下のように変更する。

```csharp
private SearchResult AspirationRootSearch(GameContext context, int depth, long prevValue)
{
    // delta 初期値の決定
    long delta;
    if (_options!.AspirationUseStageTable)
    {
        long baseDelta = _aspirationParameterTable.GetDelta(context.Stage);
        double depthFactor = AspirationParameterTable.GetDepthFactor(depth);
        delta = (long)(baseDelta * depthFactor);
    }
    else
    {
        delta = _options.AspirationDelta;
    }

    _aspirationRetryCount = 0;
    _aspirationFallbackCount = 0;
    int retryCount = 0;

    while (retryCount <= _options.AspirationMaxRetry)
    {
        long initialAlpha = prevValue - delta;
        long initialBeta = prevValue + delta;

        long alpha = Math.Max(initialAlpha, DefaultAlpha);
        long beta = Math.Min(initialBeta, DefaultBeta);

        _suppressTTStore = true;
        var result = RootSearch(context, depth, alpha, beta);

        if (result.Value <= initialAlpha)
        {
            // fail-low: 指数拡張
            delta = Math.Min(delta * (1L << (retryCount + 1)), MaxDelta);
            retryCount++;
            _aspirationRetryCount++;
        }
        else if (result.Value >= initialBeta)
        {
            // fail-high: 指数拡張
            delta = Math.Min(delta * (1L << (retryCount + 1)), MaxDelta);
            retryCount++;
            _aspirationRetryCount++;
        }
        else
        {
            _suppressTTStore = false;
            return RootSearch(context, depth, alpha, beta);
        }
    }

    // フォールバック
    _aspirationFallbackCount++;
    _suppressTTStore = false;
    return RootSearch(context, depth, DefaultAlpha, DefaultBeta);
}
```

### 5.8 AspirationParameterTable の DI 登録

`AspirationParameterTable` は Singleton として `DiProvider` に登録する。パラメータは不変であり、探索エンジンのインスタンス間で共有可能である。

`Reluca/Di/DiProvider.cs` に追加:

```csharp
services.AddSingleton<AspirationParameterTable>();
```

`PvsSearchEngine` のコンストラクタに `AspirationParameterTable` を DI で注入する。

```csharp
public PvsSearchEngine(
    ILogger<PvsSearchEngine> logger,
    MobilityAnalyzer mobilityAnalyzer,
    MoveAndReverseUpdater reverseUpdater,
    ITranspositionTable transpositionTable,
    IZobristHash zobristHash,
    MpcParameterTable mpcParameterTable,
    AspirationParameterTable aspirationParameterTable)
{
    // ... 既存の初期化 ...
    _aspirationParameterTable = aspirationParameterTable;
}
```

### 5.9 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Reluca/Search/AspirationParameterTable.cs` | **新規作成** - ステージ別 delta テーブル、深さ補正係数 |
| `Reluca/Search/SearchOptions.cs` | `AspirationUseStageTable` プロパティ追加 |
| `Reluca/Search/PvsSearchEngine.cs` | `AspirationRootSearch()` の delta 取得ロジック変更、指数拡張戦略の導入、retry 計測フィールド追加、ログ出力拡張 |
| `Reluca/Di/DiProvider.cs` | `AspirationParameterTable` の DI 登録 |

## 6. 代替案の検討 (Alternatives Considered)

### 拡張戦略

#### 案A: 指数拡張戦略（採用案）

- **概要**: fail-low/fail-high の回数に応じて拡張倍率を指数的に増大させる（retry 0: ×2, retry 1: ×4, retry 2: ×8）。
- **長所**: 連続 fail 時にフルウィンドウに素早く収束し、無駄な中間 retry を削減できる。実装が単純であり、`1L << (retryCount + 1)` で倍率を計算可能。
- **短所**: 拡張が急激であるため、最適な窓幅を飛び越える可能性がある（窓が広すぎて枝刈り効率が低下）。

#### 案B: 固定 3 倍拡張

- **概要**: fail-low/fail-high 発生時に常に delta を 3 倍にする。
- **長所**: 現在の 2 倍より拡張が速く、retry 回数が減少する。実装が最も単純。
- **短所**: 拡張倍率が一定であるため、1 回目の fail（delta が少し不足しているだけの場合）でも過度に拡張してしまう。指数拡張のような「段階的に攻撃性を上げる」戦略が取れない。

#### 案C: 非対称拡張（fail-low と fail-high で異なる拡張倍率）

- **概要**: fail-low 時は ×3（下方向は大きく拡張）、fail-high 時は ×2（上方向は控えめに拡張）のように、fail の方向によって拡張戦略を変える。
- **長所**: オセロの評価値特性（例: 序盤は下方向に振れやすい）に最適化できる可能性がある。
- **短所**: パラメータが増加し、チューニングの複雑度が上がる。オセロの評価値の非対称性がどの程度あるかが不明であり、効果が不確実。

#### 選定理由

案 A を採用する。指数拡張は実装が単純でありながら、retry 回数に応じて適切に拡張速度を調整できるバランスの良い戦略である。案 B は常に 3 倍であるため初回 fail への対応が過度であり、案 C はパラメータの増加に見合う効果が不確実である。ベンチマーク結果で問題が判明した場合に限り、案 C への移行を検討する。

### delta 初期値の設定方法

#### 案D: ステージ別テーブル（採用案）

- **概要**: ゲームステージ（序盤/中盤/終盤）に応じて異なる delta 初期値をテーブルで管理する。
- **長所**: 評価値の変動特性がステージごとに異なるというオセロの性質に適合する。テーブルの値を変更するだけでパラメータ調整が可能。
- **短所**: テーブルの初期値が最適でない可能性がある。ステージの境界をまたぐ際に delta が不連続に変化する。

#### 案E: 適応的 delta（前回反復の評価値変化量に基づく）

- **概要**: 前回反復から今回反復への評価値の変化量 `|prevValue - prevPrevValue|` を基準に、delta を動的に設定する。変化量が大きければ delta を大きく、小さければ delta を小さくする。
- **長所**: 局面の性質に自動的に適応する。テーブル管理が不要。
- **短所**: depth 2 の段階で「前回反復の変化量」が存在しないため、初回は別のロジックが必要。評価値の変化量が実際の次の反復での変化量の良い予測因子であるかが不明。depth 3 以降でしか効果を発揮せず、効果が限定的である可能性がある。

#### 選定理由

案 D を採用する。テーブルベースのアプローチは予測可能性が高く、パラメータ調整が容易である。案 E は理論的には魅力的であるが、初回の delta 設定問題があり、また評価値の変化量が次の反復での変化量を予測する信頼性が未検証である。案 D で十分な改善が得られない場合、将来的に案 E との組み合わせを検討する。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

- `AspirationParameterTable` はテーブル引きのみ（$O(1)$）であり、探索全体のコストに対する影響は無視できる。
- 深さ補正係数の計算は整数比較 2 回のみであり、コストは無視できる。
- 指数拡張戦略のコストは `1L << (retryCount + 1)` のビットシフト 1 回であり、無視できる。
- retry 計測フィールド（`_aspirationRetryCount`, `_aspirationFallbackCount`）は整数インクリメントのみであり、パフォーマンスへの影響はない。
- MPC との相互作用: MPC ON 時は浅い探索で枝刈りが発生するため、反復間の評価値の安定性が変化する可能性がある。delta の最適値は MPC ON/OFF で異なる可能性があるため、両条件下でベンチマークを実施する。

### 7.2 可観測性 (Observability)

- 構造化ログ基盤（Serilog）が導入済みであるため、Aspiration Window の retry 発生回数とフォールバック回数をログ出力する。
- 既存の探索進捗ログに `AspirationRetries` と `AspirationFallbacks` を追加する。
- これらのログにより、パラメータ調整の効果を反復ごとに確認可能となる。

### 7.3 マイグレーションと後方互換性

- `SearchOptions` のコンストラクタにデフォルト値（`aspirationUseStageTable = false`）を設定するため、既存の呼び出し元は変更不要である。
- `AspirationUseStageTable = false` の場合、既存の `AspirationDelta` がそのまま使用され、動作は完全に同一である。
- `ISearchEngine` インターフェースの変更はない。
- 破壊的変更はない。

## 8. テスト戦略 (Test Strategy)

### ユニットテスト

| テスト観点 | 内容 | テスト種別 |
|-----------|------|-----------|
| AspirationParameterTable の取得 | 各ステージで期待する delta 値が返されること | ユニット |
| 深さ補正係数の正確性 | 各深さで期待する補正係数が返されること | ユニット |
| StageTable OFF 時の非干渉 | `AspirationUseStageTable = false` 時に既存動作と同一であること | ユニット |
| StageTable ON 時の delta 変化 | `AspirationUseStageTable = true` 時にステージに応じた delta が使用されること | ユニット |
| 指数拡張戦略の動作 | fail 発生時に delta が指数的に拡張されること | ユニット |
| retry カウンタの正確性 | retry 発生時にカウンタがインクリメントされること | ユニット |

### 統合テスト

| テスト観点 | 内容 |
|-----------|------|
| NodesSearched の改善 | 同一局面で StageTable ON/OFF の NodesSearched を比較し、ON 時に改善されること |
| retry 発生率の計測 | 複数の定石局面で retry 発生率とフォールバック率を計測し、パラメータの妥当性を検証すること |
| MPC 併用時の動作 | MPC ON + Aspiration チューニング ON で既存テストが通過し、NodesSearched が改善されること |

### ベンチマーク計測項目

パラメータ調整の判断材料として、以下の計測項目を定義する。

| 指標 | 計測方法 | 目標値 |
|------|---------|--------|
| NodesSearched 削減率 | チューニング前後の NodesSearched を比較 | 正の削減率（改善） |
| retry 発生率 | `AspirationRetries / 総反復回数` | 20% 未満 |
| フォールバック率 | `AspirationFallbacks / 総反復回数` | 5% 未満 |
| 着手一致率 | チューニング前後の BestMove を比較 | 90% 以上（着手品質の非劣化） |

### delta パラメータの調整基準

| 指標 | 閾値 | アクション |
|------|------|-----------|
| retry 発生率 | 30% 以上 | 該当ステージの delta を 50% 増加する |
| retry 発生率 | 5% 未満 | 該当ステージの delta を 20% 削減する（窓を狭くして効率化） |
| フォールバック率 | 10% 以上 | MaxRetry を 1 増加し、delta を 30% 増加する |
| NodesSearched が悪化 | チューニング前より増加 | 該当ステージの delta を初期値（50）に戻し、深さ補正係数を再検討する |

調整は 1 パラメータずつ段階的に実施し、各調整後に全指標を再計測する。

### 検証方法

- 既存テストを全て実行し、`AspirationUseStageTable = false` 時に探索結果が変更されないことを確認する
- テスト用の固定局面（序盤・中盤・終盤）で `AspirationUseStageTable` ON/OFF の NodesSearched を取得し、ON 時に改善されていることを検証する
- MPC ON/OFF の両条件下で検証を実施する

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: データモデルの実装

- `AspirationParameterTable` クラスを新規作成する
- ステージ別 delta テーブルと深さ補正係数を実装する
- `AspirationParameterTable` の DI 登録を `DiProvider` に追加する
- ユニットテスト: パラメータ取得と深さ補正係数の検証

### フェーズ 2: SearchOptions の拡張と PvsSearchEngine の変更

- `SearchOptions` に `AspirationUseStageTable` プロパティを追加する
- `PvsSearchEngine` に `AspirationParameterTable` を DI 注入する
- `AspirationRootSearch()` の delta 取得ロジックを変更する（ステージ別テーブル対応）
- 指数拡張戦略を実装する
- retry 計測フィールドとログ出力を追加する
- 既存テストが全て通過することを確認する（後方互換性の検証）

### フェーズ 3: ベンチマーク検証とパラメータ調整

- 複数の局面（序盤・中盤・終盤）で retry 発生率、フォールバック率、NodesSearched を計測する
- MPC ON/OFF の両条件下で計測する
- Section 8 の「delta パラメータの調整基準」に従い、ステージ別 delta と深さ補正係数を調整する
- 調整結果をドキュメント化する

### リスク軽減策

- `AspirationUseStageTable = false` がデフォルトであるため、新しいパラメータは明示的に有効化しない限り使用されない
- パラメータ調整は `AspirationParameterTable` のみの変更で完結し、探索ロジックへの影響はない
- 指数拡張戦略の導入も `AspirationUseStageTable` フラグで制御するため、問題発生時は即座に従来の動作に戻せる

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響あり。Search コンポーネントに `AspirationParameterTable` が追加されるため、主要コンポーネント一覧を更新する必要がある。実装完了時に更新を行う。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータモデルに変更はない。Aspiration Window のパラメータ最適化は探索アルゴリズムの内部調整であり、ドメイン層には影響しない。
- `docs/api-overview.md`: 存在しない（対象外）。
