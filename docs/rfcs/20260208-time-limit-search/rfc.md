# [RFC] 時間制御付き探索の実装

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-08 |
| **タグ** | Search, 中優先度 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md), [RFC 1: nodes-searched-instrumentation](../20260205-nodes-searched-instrumentation/rfc.md), [RFC 2: multi-probcut](../20260206-multi-probcut/rfc.md) |

## 1. 要約 (Summary)

- PVS 探索エンジンの反復深化ループに時間制御を導入し、指定された制限時間内で最善手を返す機能を実装する。
- `SearchOptions` に `TimeLimitMs` パラメータを追加し、未指定時は従来通り `MaxDepth` まで探索する後方互換を維持する。
- 反復深化の各深さ完了時に経過時間を判定し、次の深さの探索を開始するかどうかを制御する。加えて、探索中のノード展開時にも定期的にタイムアウトチェックを行い、探索途中でも中断できるようにする。
- 残り手数に応じた動的な持ち時間配分戦略を実装し、序盤は短く・中盤は長く・終盤は残り時間を使い切る配分を実現する。

## 2. 背景・動機 (Motivation)

- 現在の Reluca の PVS 探索エンジンは `MaxDepth` による固定深さ制御のみを実装しており、時間制約に基づく探索制御ができない。実戦対局では持ち時間が有限であるため、時間制御なしでは以下の問題が発生する。
  - 深い探索深度を設定すると、1 手の思考に数十秒〜数分を要し、持ち時間を超過するリスクがある。
  - 浅い探索深度に固定すると、時間に余裕があっても活用できず、着手品質が不必要に制限される。
- 反復深化の構造は時間制御と相性が良い。depth=1 から順次探索するため、任意の深さで中断しても直前の深さの結果が有効な着手として利用できる。この構造的利点を活かし、時間制御を導入する。
- RFC 2（Multi-ProbCut）の導入により探索深度が大幅に向上する見込みであるが、MPC の効果は局面によって大きく異なる。時間制御がなければ、MPC の枝刈りが効きにくい局面で探索時間が予測不能に膨張するリスクがある。
- WZebra は持ち時間配分を実装しており、Reluca が WZebra 水準の実戦運用を達成するには時間制御が不可欠である。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- 指定した制限時間内に探索が完了し、有効な着手が返されること
- 制限時間が短い場合（例: 100ms）でも最低 depth=1 の探索結果が保証されること
- `TimeLimitMs` 未指定時は従来通り `MaxDepth` まで探索すること（後方互換）
- 残り手数に応じた動的な持ち時間配分により、対局全体を通じて持ち時間を効率的に使い切ること
- 探索途中での中断により、制限時間を大幅に超過することがないこと（超過幅の目標: 100ms 以内）

### やらないこと (Non-Goals)

- 対手の思考時間を考慮した配分戦略（ポンダリング）。本 RFC では自手の持ち時間配分のみを扱う
- 秒読み（バイヨミ）制への対応。本 RFC ではフィッシャー方式（持ち時間一括）を前提とする
- `CancellationToken` による外部からの非同期キャンセル機構。本 RFC ではエンジン内部の時間管理のみを実装する
- 並列探索との統合。時間制御はシングルスレッド前提で設計する

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- RFC 1（nodes-searched-instrumentation）が完了していること。時間制御の効果は NodesSearched と経過時間の両面で評価するため、計測基盤が必須である（実装済み）
- PVS 探索エンジン（`PvsSearchEngine`）が反復深化を実装済みであること（実装済み）
- `System.Diagnostics.Stopwatch` が利用可能であること（.NET 8.0 標準ライブラリ。追加依存なし）

## 5. 詳細設計 (Detailed Design)

### 5.1 時間制御の全体アーキテクチャ

時間制御は以下の 2 レイヤーで構成する。

```
┌─────────────────────────────────────────────────┐
│ レイヤー 1: 反復深化ループの制御                │
│  - 各深さの探索完了後に経過時間を判定           │
│  - 次の深さの推定時間が残り時間を超える場合中断 │
└─────────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────┐
│ レイヤー 2: ノード展開時のタイムアウトチェック   │
│  - 一定ノード数ごとに経過時間を確認             │
│  - タイムアウト時は例外で探索を中断             │
└─────────────────────────────────────────────────┘
```

レイヤー 1 は「次の深さを探索するかどうか」を判断する粗い粒度の制御である。レイヤー 2 は「現在進行中の探索を途中で打ち切る」ための細かい粒度の制御であり、レイヤー 1 で開始を許可した探索が制限時間を大幅に超過することを防止する。

### 5.2 SearchOptions の拡張

`Reluca/Search/SearchOptions.cs` に時間制御パラメータを追加する。

```csharp
public class SearchOptions
{
    // ... 既存プロパティ ...

    /// <summary>
    /// 探索の制限時間（ミリ秒）。
    /// null の場合は時間制限なしで MaxDepth まで探索する。
    /// </summary>
    public long? TimeLimitMs { get; }

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
        bool useMultiProbCut = false,
        long? timeLimitMs = null)
    {
        MaxDepth = maxDepth;
        UseTranspositionTable = useTranspositionTable;
        UseAspirationWindow = useAspirationWindow;
        AspirationDelta = aspirationDelta;
        AspirationMaxRetry = aspirationMaxRetry;
        AspirationUseStageTable = aspirationUseStageTable;
        UseMultiProbCut = useMultiProbCut;
        TimeLimitMs = timeLimitMs;
    }
}
```

`TimeLimitMs` を `long?`（nullable）とすることで、未指定時は従来動作を維持する。`int` ではなく `long` とする理由は、長時間の対局（持ち時間 60 分 = 3,600,000ms 等）を扱う際の安全性を確保するためである。

### 5.3 SearchResult の拡張

`Reluca/Search/SearchResult.cs` に探索の到達深さと経過時間を追加する。

```csharp
public class SearchResult
{
    // ... 既存プロパティ ...

    /// <summary>
    /// 探索が完了した最大深さ
    /// </summary>
    public int CompletedDepth { get; }

    /// <summary>
    /// 探索の経過時間（ミリ秒）
    /// </summary>
    public long ElapsedMs { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SearchResult(
        int bestMove,
        long value,
        long nodesSearched = 0,
        int completedDepth = 0,
        long elapsedMs = 0)
    {
        BestMove = bestMove;
        Value = value;
        NodesSearched = nodesSearched;
        CompletedDepth = completedDepth;
        ElapsedMs = elapsedMs;
    }
}
```

`CompletedDepth` は時間制御により探索が途中で中断された場合に、実際に完了した深さを報告するために使用する。`ElapsedMs` は探索のパフォーマンス分析に使用する。

### 5.4 タイムアウト中断用の例外クラス

探索途中でのタイムアウト中断を実現するため、専用の例外クラスを新設する。

ファイル: `Reluca/Search/SearchTimeoutException.cs`

```csharp
/// <summary>
/// 探索の制限時間超過時にスローされる例外。
/// PVS 探索の再帰呼び出しを一括で中断するために使用する。
/// 通常の例外処理フローとは異なり、探索エンジン内部でのみ catch される。
/// </summary>
public class SearchTimeoutException : Exception
{
    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SearchTimeoutException()
        : base("探索の制限時間を超過しました。")
    {
    }
}
```

### 5.5 PvsSearchEngine の時間制御統合

#### 5.5.1 インスタンスフィールドの追加

```csharp
/// <summary>
/// 探索の開始時刻を計測するストップウォッチ
/// </summary>
private readonly Stopwatch _stopwatch = new();

/// <summary>
/// 探索の制限時間（ミリ秒）。null は制限なし
/// </summary>
private long? _timeLimitMs;

/// <summary>
/// タイムアウトチェックのノード間隔。
/// この値ごとに Stopwatch を確認する。
/// </summary>
private const int TimeoutCheckInterval = 4096;
```

#### 5.5.2 反復深化ループの時間制御（レイヤー 1）

`Search()` メソッドの反復深化ループに時間制御を組み込む。

```csharp
public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
{
    _bestMove = -1;
    _evaluator = evaluator;
    _options = options;
    _mpcEnabled = options.UseMultiProbCut;
    _timeLimitMs = options.TimeLimitMs;

    // ストップウォッチ開始
    _stopwatch.Restart();

    // TT クリア
    if (options.UseTranspositionTable)
    {
        _transpositionTable.Clear();
    }

    SearchResult result = new SearchResult(-1, 0);
    long prevValue = 0;
    long totalNodesSearched = 0;
    int completedDepth = 0;
    long prevDepthElapsedMs = 0;

    for (int depth = 1; depth <= options.MaxDepth; depth++)
    {
        // レイヤー 1: 次の深さを探索するか判定
        if (depth >= 2 && _timeLimitMs.HasValue)
        {
            long elapsedMs = _stopwatch.ElapsedMilliseconds;
            long remainingMs = _timeLimitMs.Value - elapsedMs;

            // 前回の深さにかかった時間から次の深さの所要時間を推定
            // 分岐係数を考慮し、次の深さは前回の約 3 倍の時間を要すると推定
            long estimatedNextMs = prevDepthElapsedMs * 3;

            if (estimatedNextMs > remainingMs)
            {
                _logger.LogInformation(
                    "時間制御により探索を打ち切り {@TimeControl}",
                    new
                    {
                        CompletedDepth = completedDepth,
                        ElapsedMs = elapsedMs,
                        TimeLimitMs = _timeLimitMs.Value,
                        EstimatedNextMs = estimatedNextMs,
                        RemainingMs = remainingMs
                    });
                break;
            }
        }

        _currentDepth = depth;
        _nodesSearched = 0;
        _mpcCutCount = 0;
        _aspirationRetryCount = 0;
        _aspirationFallbackCount = 0;

        long depthStartMs = _stopwatch.ElapsedMilliseconds;

        try
        {
            SearchResult depthResult;
            if (depth >= 2 && options.UseAspirationWindow)
            {
                depthResult = AspirationRootSearch(context, depth, prevValue);
            }
            else
            {
                depthResult = RootSearch(context, depth, DefaultAlpha, DefaultBeta);
            }

            // 探索成功: 結果を更新
            result = depthResult;
            completedDepth = depth;
        }
        catch (SearchTimeoutException)
        {
            // レイヤー 2: 探索途中でタイムアウト
            // 直前の深さの結果を返す（result は更新しない）
            _logger.LogInformation(
                "探索途中でタイムアウト {@Timeout}",
                new
                {
                    InterruptedDepth = depth,
                    CompletedDepth = completedDepth,
                    ElapsedMs = _stopwatch.ElapsedMilliseconds,
                    TimeLimitMs = _timeLimitMs.Value
                });
            break;
        }

        long depthElapsedMs = _stopwatch.ElapsedMilliseconds - depthStartMs;
        prevDepthElapsedMs = depthElapsedMs;

        totalNodesSearched += _nodesSearched;
        _logger.LogInformation("探索進捗 {@SearchProgress}", new
        {
            Depth = depth,
            Nodes = _nodesSearched,
            TotalNodes = totalNodesSearched,
            Value = result.Value,
            MpcCuts = _mpcCutCount,
            AspirationRetries = _aspirationRetryCount,
            AspirationFallbacks = _aspirationFallbackCount,
            DepthElapsedMs = depthElapsedMs
        });

        prevValue = result.Value;
    }

    _stopwatch.Stop();

    return new SearchResult(
        result.BestMove,
        result.Value,
        totalNodesSearched,
        completedDepth,
        _stopwatch.ElapsedMilliseconds);
}
```

#### 5.5.3 ノード展開時のタイムアウトチェック（レイヤー 2）

`Pvs()` メソッドにタイムアウトチェックを追加する。全ノードで `Stopwatch` を確認すると性能に影響するため、`TimeoutCheckInterval` ノードごとに確認する。

```csharp
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
{
    _nodesSearched++;

    // タイムアウトチェック（一定ノード数ごと）
    if (_timeLimitMs.HasValue && (_nodesSearched & (TimeoutCheckInterval - 1)) == 0)
    {
        if (_stopwatch.ElapsedMilliseconds >= _timeLimitMs.Value)
        {
            throw new SearchTimeoutException();
        }
    }

    // 以降は既存の処理...
}
```

**チェック頻度の設計根拠**: `TimeoutCheckInterval = 4096`（2 のべき乗）とする。ビット AND によるモジュロ演算で高速に判定できる。オセロの探索において 1 ノードあたりの処理時間は概ね 1-10 マイクロ秒程度であるため、4096 ノードごとのチェックは約 4-40ms 間隔となる。制限時間の超過幅は最大で 1 チェック間隔分（約 40ms）に抑えられ、100ms 以内の目標を達成できる。

**[仮定]** `TimeoutCheckInterval = 4096` は初期値であり、実測に基づいて調整する可能性がある。チェック間隔が大きすぎると制限時間の超過幅が拡大し、小さすぎると `Stopwatch` のアクセスオーバーヘッドが探索性能に影響する。

### 5.6 持ち時間配分戦略

#### 5.6.1 TimeAllocator クラス

対局全体の持ち時間から、各手番に割り当てる制限時間を計算するクラスを新設する。

ファイル: `Reluca/Search/TimeAllocator.cs`

```csharp
/// <summary>
/// 対局全体の持ち時間を各手番に配分する。
/// 残り手数に応じた動的な配分戦略を実装する。
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
    /// <param name="remainingTimeMs">残り持ち時間（ミリ秒）</param>
    /// <param name="turnCount">現在のターン数（0〜59）</param>
    /// <returns>今回の手番に割り当てる制限時間（ミリ秒）</returns>
    public long Allocate(long remainingTimeMs, int turnCount)
    {
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
    /// 残り手数を推定する。パスによる手数変動を考慮し、やや保守的に推定する。
    /// </summary>
    /// <param name="turnCount">現在のターン数</param>
    /// <returns>推定残り手数</returns>
    private static int EstimateRemainingMoves(int turnCount)
    {
        int remaining = MaxTurns - turnCount;

        // 自分の手番のみをカウント（2 で割る）
        // パスの可能性を考慮し、やや多めに見積もる
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
```

#### 5.6.2 配分戦略の設計根拠

オセロの思考時間配分は以下の特性を考慮する。

- **序盤（ターン 0〜15）**: 定石による知識で対応できる局面が多く、深い探索の必要性が相対的に低い。時間を節約し中盤に回す。
- **中盤（ターン 16〜44）**: 局面の複雑度が最も高く、探索深度が着手品質に直結する。持ち時間の大部分をここに配分する。
- **終盤（ターン 45〜59）**: `FindBestMover` が depth=99（完全読み切り）に切り替えるため、探索は残り空きマス数に依存する。MPC や置換表の効果で高速に解ける局面が多いが、一部の局面は長時間を要する可能性がある。完全読み切りモードの時間制御については将来の課題とし、本 RFC では通常探索時の配分を実装する。

**[仮定]** フェーズ係数（0.8 / 1.3 / 0.9）は初期値であり、実戦対局のログ分析に基づいて調整する。フェーズ境界（ターン 15/44）は `FindBestMover` の `EndgameTurnThreshold = 46` および `Stage` の区分と整合するよう設定した。

#### 5.6.3 FindBestMover との統合

`FindBestMover` が `TimeAllocator` を使用して各手番の制限時間を決定する例を示す。

```csharp
public class FindBestMover : IMovable
{
    // ... 既存フィールド ...

    /// <summary>
    /// 時間配分器
    /// </summary>
    private TimeAllocator? _timeAllocator;

    /// <summary>
    /// 残り持ち時間（ミリ秒）。外部から設定する
    /// </summary>
    public long? RemainingTimeMs { get; set; }

    public int Move(GameContext context)
    {
        SearchEngine = DiProvider.Get().GetService<ISearchEngine>();

        IEvaluable evaluator;
        int depth;
        if (context.TurnCount >= EndgameTurnThreshold)
        {
            evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
            depth = EndgameDepth;
        }
        else
        {
            evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
            depth = NormalDepth;
        }

        // 時間制限の計算
        long? timeLimitMs = null;
        if (RemainingTimeMs.HasValue)
        {
            _timeAllocator ??= new TimeAllocator();
            timeLimitMs = _timeAllocator.Allocate(
                RemainingTimeMs.Value, context.TurnCount);
        }

        var options = new SearchOptions(
            depth,
            useTranspositionTable: true,
            useAspirationWindow: true,
            aspirationUseStageTable: true,
            useMultiProbCut: true,
            timeLimitMs: timeLimitMs
        );
        var result = SearchEngine.Search(context, options, evaluator);

        return result.BestMove;
    }
}
```

**[仮定]** `RemainingTimeMs` は外部（UI 層やプロトコルアダプタ層）から設定される前提とする。本 RFC では `FindBestMover` にプロパティとして公開するが、将来的にはプロトコル層（例: UCI/USI プロトコル対応時）からの自動設定を想定している。

### 5.7 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Reluca/Search/SearchOptions.cs` | `TimeLimitMs` プロパティ追加 |
| `Reluca/Search/SearchResult.cs` | `CompletedDepth`, `ElapsedMs` プロパティ追加 |
| `Reluca/Search/SearchTimeoutException.cs` | **新規作成** - タイムアウト例外クラス |
| `Reluca/Search/TimeAllocator.cs` | **新規作成** - 持ち時間配分クラス |
| `Reluca/Search/PvsSearchEngine.cs` | 時間制御の統合（Stopwatch、レイヤー 1/2 の実装、ログ拡張） |
| `Reluca/Movers/FindBestMover.cs` | `RemainingTimeMs` プロパティ追加、`TimeAllocator` 統合 |

## 6. 代替案の検討 (Alternatives Considered)

### タイムアウト中断の実装方式

#### 案A: 例外によるスタック巻き戻し（採用案）

- **概要**: タイムアウト検出時に `SearchTimeoutException` をスローし、再帰呼び出しスタックを一括で巻き戻す。`Search()` メソッドの反復深化ループで catch し、直前の深さの結果を返す。
- **長所**: 既存の `Pvs()` メソッドの戻り値やロジックを変更する必要がない。再帰の深さに関係なく即座に中断できる。実装がシンプルで、既存コードへの侵襲が最小限である。
- **短所**: 例外のスロー/キャッチにはオーバーヘッドがある。ただし、タイムアウトは 1 回の探索で最大 1 回しか発生しないため、性能への影響は無視できる。

#### 案B: フラグベースの中断

- **概要**: インスタンスフィールド `_timeout` を導入し、タイムアウト検出時に `true` に設定する。`Pvs()` メソッドの各所で `_timeout` をチェックし、`true` の場合は即座に `return` する。
- **長所**: 例外のオーバーヘッドがない。
- **短所**: `Pvs()` メソッドの複数箇所にフラグチェックを挿入する必要があり、コードの可読性が低下する。フラグチェックの挿入漏れがあると、タイムアウト後も探索が継続するバグが発生する。戻り値の解釈が複雑化する（タイムアウト時の戻り値は無効だが、型としては `long` が返される）。MPC の浅い探索中のタイムアウト時に、フラグの退避・復元が `_mpcEnabled` と同様に必要になり複雑化する。

#### 選定理由

案 A を採用する。例外方式は「異常終了」のセマンティクスと合致し、通常のフローと中断フローが明確に分離される。フラグ方式は挿入漏れリスクがあり、特に MPC の浅い探索内での中断処理が複雑になる。例外のオーバーヘッドは 1 回の探索あたり最大 1 回であり、実質的に性能影響はない。

### 次の深さの探索可否判定

#### 案C: 前回深さの所要時間に基づく推定（採用案）

- **概要**: 前回の深さの探索にかかった時間を記録し、次の深さは約 3 倍の時間を要すると推定する。推定時間が残り時間を超える場合は次の深さを開始しない。
- **長所**: 実装がシンプルで、実測値に基づくため局面の特性が反映される。MPC の枝刈り効果が反映された実時間で判定できる。
- **短所**: 分岐係数 3 倍の仮定が外れる局面では、残り時間を有効活用できない（過小推定）、または制限時間を超過する（過大推定）可能性がある。ただし、レイヤー 2 のタイムアウトチェックが安全ネットとして機能する。

#### 案D: NPS ベースの推定

- **概要**: 前回の深さの探索で得られた NPS（Nodes Per Second）と、次の深さの予想ノード数から所要時間を推定する。
- **長所**: より精密な推定が可能。
- **短所**: 次の深さの予想ノード数を正確に見積もることが困難である。MPC の効果により実際のノード数は予想から大きく乖離する可能性がある。実装が複雑化する。

#### 選定理由

案 C を採用する。MPC の存在により次の深さのノード数予測は信頼性が低く、NPS ベースの推定（案 D）は複雑さに見合う精度向上が見込めない。実測時間ベースの推定は MPC の枝刈り効果が自動的に反映されるため、MPC ON/OFF の両方に対応できる。分岐係数 3 倍の仮定はオセロの平均分岐係数（約 10）と Alpha-Beta の理想的枝刈り（分岐係数の平方根）から概ね妥当であるが、レイヤー 2 が安全ネットとなるため、推定の精度が多少低くても問題ない。

**[仮定]** 分岐係数 3 倍は初期値であり、実測データに基づいて調整する。MPC ON 時は実効分岐係数が低下するため、3 倍は保守的（過大推定寄り）である。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

- `Stopwatch` のアクセスは `TimeoutCheckInterval = 4096` ノードごとに制限するため、探索性能への影響は微小である。`Stopwatch.ElapsedMilliseconds` の呼び出しコストは数十ナノ秒程度であり、4096 ノードに 1 回のアクセスでは探索全体の 0.01% 未満のオーバーヘッドに収まる。
- タイムアウト例外のスロー/キャッチは 1 回の探索で最大 1 回であり、探索性能に影響しない。
- `TimeLimitMs` 未指定時はタイムアウトチェックのビット AND 条件が `false` になるだけであり、実質的にゼロコストである。

### 7.2 可観測性 (Observability)

- 反復深化の各深さ完了時のログに `DepthElapsedMs` を追加し、各深さの所要時間を記録する。
- 時間制御による打ち切り時に専用のログメッセージを出力する（レイヤー 1/2 それぞれ）。
- `SearchResult` に `CompletedDepth` と `ElapsedMs` を追加し、呼び出し元が探索の到達度と所要時間を取得できるようにする。

### 7.3 マイグレーションと後方互換性

- `SearchOptions` のコンストラクタにデフォルト値（`timeLimitMs: null`）を設定するため、既存の呼び出し元は変更不要である。
- `SearchResult` のコンストラクタにデフォルト値（`completedDepth: 0`, `elapsedMs: 0`）を設定するため、既存のテストコードは変更不要である。
- `TimeLimitMs` 未指定時は既存の探索動作と完全に同一である。
- `ISearchEngine` インターフェースの変更はない。
- 破壊的変更はない。

## 8. テスト戦略 (Test Strategy)

### ユニットテスト

| テスト観点 | 内容 | テスト種別 |
|-----------|------|-----------|
| 時間制限なしの非干渉 | `TimeLimitMs = null` 時の `BestMove` と `Value` が従来と一致すること | ユニット |
| 時間制限内の完了 | `TimeLimitMs` を十分に大きく設定した場合、`MaxDepth` まで探索が完了すること | ユニット |
| 時間制限による打ち切り | `TimeLimitMs` を短く設定した場合、制限時間付近で探索が中断し、有効な着手が返されること | ユニット |
| 最低 1 手の保証 | `TimeLimitMs` を極端に短く設定（例: 1ms）した場合でも、depth=1 の結果が返されること | ユニット |
| CompletedDepth の正確性 | 時間制限で打ち切られた場合、`CompletedDepth` が実際に完了した深さを正しく報告すること | ユニット |
| TimeAllocator の配分 | 各ターン数で `Allocate()` が妥当な制限時間を返すこと | ユニット |
| TimeAllocator の最低保証 | 残り時間が極小の場合でも `MinTimeLimitMs` 以上が返されること | ユニット |
| TimeAllocator のフェーズ係数 | 序盤・中盤・終盤で異なる配分比率が適用されること | ユニット |
| SearchTimeoutException の中断 | 探索途中でタイムアウトが発生した場合、直前の深さの結果が使用されること | ユニット |

### 統合テスト

| テスト観点 | 内容 |
|-----------|------|
| 制限時間の遵守 | 複数の局面で `TimeLimitMs` を設定し、実際の経過時間が制限時間の 1.1 倍以内に収まること |
| MPC + 時間制御の組み合わせ | MPC ON + 時間制限ありの状態で、正しく動作すること |
| FindBestMover との統合 | `RemainingTimeMs` を設定した `FindBestMover` が、時間制限付きで最善手を返すこと |

### 検証方法

- 既存テストを全て実行し、`TimeLimitMs = null` 時に探索結果が変更されないことを確認する
- テスト用の固定局面で `TimeLimitMs` を 500ms, 1000ms, 5000ms に設定し、各制限時間の到達深さと実際の経過時間を記録する
- `CompletedDepth` が `TimeLimitMs` に応じて変動することを検証する

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: SearchOptions / SearchResult の拡張

- `SearchOptions` に `TimeLimitMs` プロパティを追加する
- `SearchResult` に `CompletedDepth`, `ElapsedMs` プロパティを追加する
- `SearchTimeoutException` クラスを新規作成する
- 既存テストが全て通過することを確認する（後方互換性の検証）

### フェーズ 2: PvsSearchEngine への時間制御統合

- `PvsSearchEngine` に `Stopwatch`, `_timeLimitMs`, `TimeoutCheckInterval` を追加する
- レイヤー 1（反復深化ループの制御）を実装する
- レイヤー 2（ノード展開時のタイムアウトチェック）を実装する
- ユニットテスト: 時間制限なしの非干渉、時間制限による打ち切り、最低 1 手の保証

### フェーズ 3: TimeAllocator の実装

- `TimeAllocator` クラスを新規作成する
- `FindBestMover` に `RemainingTimeMs` プロパティと `TimeAllocator` 統合を追加する
- ユニットテスト: 配分計算の検証、最低保証時間、フェーズ係数

### フェーズ 4: 統合検証とパラメータ調整

- 複数の局面で時間制御の動作を検証し、制限時間の遵守を確認する
- `TimeoutCheckInterval` の妥当性を実測で検証し、必要に応じて調整する
- 分岐係数推定値（3 倍）の妥当性を実測で検証し、必要に応じて調整する
- フェーズ係数（0.8 / 1.3 / 0.9）の妥当性を対局ログで検証し、必要に応じて調整する

### リスク軽減策

- `TimeLimitMs = null` がデフォルトであるため、時間制御は明示的に有効化しない限り動作しない
- レイヤー 2 の例外による中断は安全ネットであり、通常はレイヤー 1 の判定で探索が終了する
- `TimeoutCheckInterval` はコンパイル時定数であるが、将来的にコンストラクタパラメータ化も容易である

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響あり。Search コンポーネントに `TimeAllocator` が追加されるため、主要コンポーネント一覧を更新する必要がある。`SearchOptions` の説明にも時間制御パラメータの追記が必要である。実装完了時に更新を行う。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータモデルに変更はない。時間制御は探索アルゴリズムの実行制御であり、ドメイン層には影響しない。
- `docs/api-overview.md`: 存在しない（対象外）。
