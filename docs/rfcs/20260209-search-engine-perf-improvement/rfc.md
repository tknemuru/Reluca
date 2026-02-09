# [RFC] 探索エンジン パフォーマンス改善（Phase 1 & Phase 2）

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-09 |
| **タグ** | Search, Evaluates, Analyzers, Performance, P0-P1 |
| **関連リンク** | `docs/reports/20260209-report-search-performance-bottleneck.md` |

## 1. 要約 (Summary)

- WZebra 評価テーブル組み込み後、探索エンジンの実行速度が大幅に低下している。評価関数が重くなったことで、従来から存在していた構造的な非効率（毎回のヒープアロケーション、64 マスフルスキャン、LINQ 使用等）が顕在化した。
- 本 RFC では、Phase 1（即効性のある改善・変更範囲が限定的）と Phase 2（構造的な改善・効果大・変更範囲大）の 2 段階に分けた改善計画を提案する。
- Phase 1 では DI コンテナ呼び出し排除、カウント専用メソッド追加、LINQ 排除、アロケーション排除の 4 項目を実施し、Phase 2 では MakeMove/UnmakeMove パターン導入、値型化、Null Window Search 実装、Aspiration 二重探索排除の 4 項目を実施する。
- 探索ノード 10 万ノードの場合に推定 300〜500 万個発生する GC オブジェクトを大幅に削減し、Gen0/Gen1 GC の頻発を解消することが目標である。

## 2. 背景・動機 (Motivation)

- Windows 環境での実対局において、WZebra 評価テーブル組み込み前の旧ロジックと比較して探索速度が大幅に低下している。
- 原因は評価関数の重量化そのものではなく、評価関数が重くなった結果として、既存の構造的な非効率が支配的なボトルネックとして顕在化した点にある。旧ロジックでは評価関数が軽量だったため問題が目立たなかっただけで、ボトルネックの構造は以前から存在していた。
- 具体的なボトルネックは以下の通りである:
  - **MakeMove での毎回 DeepCopy**: `GameContext`/`BoardContext` は `record`（参照型）であり、`with` 式のたびにヒープアロケーションが発生する。探索ノード数が数万〜数十万に達するため、同数の GC オブジェクトが生成される。
  - **MobilityAnalyzer.Analyze**: 毎回 `new List<int>()` のアロケーション、64 マス全スキャン、`DiProvider.Get().GetService` の毎回呼び出し。1 ノードあたり 3 回呼ばれる。
  - **FeaturePatternExtractor.Extract**: パターン 11 種類分の `Dictionary` + `List<int>` x 11 が毎回ヒープ生成される。
  - **OrderMoves の LINQ**: `OrderByDescending` 内で MakeMove + Evaluate をフル実行し、デリゲートオブジェクト + ソート用バッファ + `.ToList()` のアロケーションが発生する。
  - **PVS の Null Window Search 未実装**: 名前は `Pvs` だが、2 手目以降もフルウィンドウで探索しており、枝刈り効率が大幅に低下している。
  - **Aspiration 成功時の二重 RootSearch**: TT Store 有効化のために同一探索を 2 回実行している。
- 1 ノードあたり約 30〜50 の GC オブジェクトが生成され、10 万ノード探索時に 300〜500 万の GC オブジェクトが生成される。これが Gen0/Gen1 GC の頻発を引き起こし、大幅な遅延の原因となっている。
- 放置した場合、探索深度の増加に伴い問題はさらに深刻化し、対局時の思考時間が実用的でないレベルに達するリスクがある。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- GC オブジェクト生成量を 1 ノードあたり 30〜50 個から大幅に削減する（Phase 1 で半減、Phase 2 で実質ゼロを目指す）
- 探索速度を WZebra 評価テーブル組み込み前と同等以上に回復させる
- Null Window Search の実装により枝刈り効率を改善し、同一時間でより深い探索を可能にする
- Aspiration Window の二重探索を排除し、反復深化 1 深さあたりの所要時間を短縮する

### やらないこと (Non-Goals)

- ビットボードによる高速合法手生成（Phase 3 として将来課題とする）
- Zobrist ハッシュの差分更新（Phase 3 として将来課題とする）
- `BitOperations.PopCount()` の利用（Phase 3 として将来課題とする）
- 評価関数の差分更新（Phase 3 として将来課題とする）
- マルチスレッド探索の導入
- 評価関数自体の精度・構造の変更

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- .NET 8.0 / C# 12 環境であること
- `record struct` は C# 10 以降で利用可能であり、現行の C# 12 環境で問題なく使用できる
- Phase 2 の `BoardContext` の `record struct` 化は、`BoardContext` を参照する全コンポーネントに影響するため、Phase 1 完了後に着手する
- Phase 2 の Null Window Search 実装は探索アルゴリズムの変更であり、既存のテスト結果との整合性検証が必要である
- `dotnet-trace` / `dotnet-counters` による GC 回数・アロケーション量の定量計測を Phase 1 実施前後に行い、効果を検証する

## 5. 詳細設計 (Detailed Design)

### 5.1 Phase 1: 即効性のある改善

#### 5.1.1 DI コンテナ呼び出し排除

**対象ファイル**: `Reluca/Analyzers/MobilityAnalyzer.cs`

現状、`Analyze` メソッド内で毎回 `DiProvider.Get().GetService<MoveAndReverseUpdater>()` を呼び出している。探索中に 1 ノードあたり 3 回呼ばれるため、数十万回の DI コンテナ解決が発生している。

**変更内容**: コンストラクタインジェクションで `MoveAndReverseUpdater` を保持するように変更する。

```csharp
// 変更前
public class MobilityAnalyzer
{
    public List<int> Analyze(GameContext context, Disc.Color turn)
    {
        // ...
        var updater = DiProvider.Get().GetService<MoveAndReverseUpdater>();
        // ...
    }
}

// 変更後
public class MobilityAnalyzer
{
    private readonly MoveAndReverseUpdater _updater;

    public MobilityAnalyzer(MoveAndReverseUpdater updater)
    {
        _updater = updater;
    }

    public List<int> Analyze(GameContext context, Disc.Color turn)
    {
        // _updater を直接使用
        // ...
    }
}
```

**影響範囲**: `MobilityAnalyzer` のインスタンス生成箇所（DI 登録）の変更が必要。`PvsSearchEngine` は既にコンストラクタインジェクションで `MobilityAnalyzer` を受け取っているため、探索エンジン側の変更は不要である。

#### 5.1.2 MobilityAnalyzer.AnalyzeCount メソッド追加

**対象ファイル**: `Reluca/Analyzers/MobilityAnalyzer.cs`, `Reluca/Evaluates/FeaturePatternEvaluator.cs`

`FeaturePatternEvaluator.Evaluate` 内では `MobilityAnalyzer.Analyze(context, color).Count` のように合法手のリストを生成した直後に `.Count` しか参照していない。リスト全体のアロケーションは不要である。

**変更内容**: カウントのみを返す `AnalyzeCount` メソッドを追加する。

```csharp
// MobilityAnalyzer に追加
public int AnalyzeCount(GameContext context, Disc.Color turn)
{
    Debug.Assert(context != null);
    Debug.Assert(context.Turn != Disc.Color.Undefined);

    var orgTurn = context.Turn;
    if (turn == Disc.Color.Undefined)
    {
        turn = context.Turn;
    }

    try
    {
        context.Turn = turn;
        context.Mobility = 0ul;
        int count = 0;
        for (var i = 0; i < Board.AllLength; i++)
        {
            if (_updater.Update(context, i))
            {
                count++;
            }
        }
        return count;
    }
    finally
    {
        context.Turn = orgTurn;
    }
}
```

```csharp
// FeaturePatternEvaluator.Evaluate 内の変更
// 変更前
var black = MobilityAnalyzer.Analyze(context, Disc.Color.Black).Count;
var white = MobilityAnalyzer.Analyze(context, Disc.Color.White).Count;

// 変更後
var black = MobilityAnalyzer.AnalyzeCount(context, Disc.Color.Black);
var white = MobilityAnalyzer.AnalyzeCount(context, Disc.Color.White);
```

**効果**: 1 ノードあたり 2 回の `List<int>` アロケーションを排除する。

#### 5.1.3 OrderMoves の LINQ 排除

**対象ファイル**: `Reluca/Search/PvsSearchEngine.cs`

現状、`OrderMoves` メソッドで LINQ の `OrderByDescending` を使用しており、デリゲートオブジェクト、ソート用内部バッファ、`.ToList()` による新規リスト生成が毎回発生している。

**変更内容**: 固定長配列 + インラインソート（挿入ソート）に置き換える。オセロの合法手数は最大でも約 30 手程度であり、挿入ソートで十分である。

```csharp
// 変更後
private List<int> OrderMoves(List<int> moves, GameContext context)
{
    // 評価値を格納する配列（スタック上に確保可能な範囲）
    int count = moves.Count;
    Span<long> scores = stackalloc long[count];
    Span<int> indices = stackalloc int[count];

    for (int i = 0; i < count; i++)
    {
        var child = MakeMove(context, moves[i]);
        BoardAccessor.Pass(child);
        scores[i] = Evaluate(child);
        indices[i] = i;
    }

    // 挿入ソート（降順）
    for (int i = 1; i < count; i++)
    {
        long keyScore = scores[i];
        int keyIndex = indices[i];
        int j = i - 1;
        while (j >= 0 && scores[j] < keyScore)
        {
            scores[j + 1] = scores[j];
            indices[j + 1] = indices[j];
            j--;
        }
        scores[j + 1] = keyScore;
        indices[j + 1] = keyIndex;
    }

    // ソート結果を反映
    var sorted = new List<int>(count);
    for (int i = 0; i < count; i++)
    {
        sorted.Add(moves[indices[i]]);
    }
    return sorted;
}
```

**[仮定]** 合法手数が `stackalloc` で安全に確保できる範囲（最大 64）に収まることを前提としている。オセロの理論的最大合法手数は盤面の空きマス数以下であるため、この前提は成立する。

**効果**: LINQ のデリゲートオブジェクト、内部イテレータ、ソートバッファのアロケーションを排除する。

#### 5.1.4 FeaturePatternExtractor.Extract のアロケーション排除

**対象ファイル**: `Reluca/Evaluates/FeaturePatternExtractor.cs`

現状、`Extract` メソッドは毎回 `Dictionary<FeaturePattern.Type, List<int>>` を新規作成し、パターン 11 種類分の `List<int>` をヒープにアロケーションしている。

**変更内容**: 事前確保した配列にインデックスを書き込む方式に変更する。パターンの種類と各パターンのサブパターン数は固定であるため、フィールドとして事前確保できる。

```csharp
public class FeaturePatternExtractor
{
    // 事前確保した抽出結果格納用の配列
    // PatternResults[patternType] = int[] (各サブパターンのインデックス)
    private readonly Dictionary<FeaturePattern.Type, int[]> _preallocatedResults;

    public FeaturePatternExtractor()
    {
        // ... 既存の初期化 ...

        // 事前確保
        _preallocatedResults = new Dictionary<FeaturePattern.Type, int[]>();
        foreach (var pattern in PatternPositions)
        {
            _preallocatedResults[pattern.Key] = new int[pattern.Value.Count];
        }
    }

    /// <summary>
    /// 特徴パターンを抽出する（アロケーションなし版）。
    /// 戻り値は内部バッファへの参照であり、次回呼び出しで上書きされる。
    /// </summary>
    public Dictionary<FeaturePattern.Type, int[]> ExtractNoAlloc(BoardContext context)
    {
        foreach (var pattern in PatternPositions)
        {
            var arr = _preallocatedResults[pattern.Key];
            for (int i = 0; i < pattern.Value.Count; i++)
            {
                arr[i] = ConvertToTernaryIndex(context, pattern.Value[i]);
            }
        }
        return _preallocatedResults;
    }
}
```

**[仮定]** 探索はシングルスレッドで実行される前提であるため、内部バッファの共有による競合は発生しない。`PvsSearchEngine` のモジュール Doc コメントにも「シングルスレッド前提」と明記されている。

**影響範囲**: `FeaturePatternEvaluator.Evaluate` 内の `Extractor.Extract` 呼び出しを `ExtractNoAlloc` に変更する必要がある。戻り値の型が `Dictionary<FeaturePattern.Type, List<int>>` から `Dictionary<FeaturePattern.Type, int[]>` に変わるため、利用側のイテレーション処理を修正する。

**効果**: 1 ノードあたり `Dictionary` 1 個 + `List<int>` 11 個 = 12 個の GC オブジェクト生成を排除する。

### 5.2 Phase 2: 構造的な改善

#### 5.2.1 MakeMove/UnmakeMove パターンへの移行

**対象ファイル**: `Reluca/Search/PvsSearchEngine.cs`, `Reluca/Updaters/MoveAndReverseUpdater.cs`

現状、`MakeMove` は `BoardAccessor.DeepCopy` で盤面を丸ごとコピーし、新しい `GameContext` を返している。探索ノードごとに `GameContext` + `BoardContext` の 2 オブジェクトがヒープに生成される。

**変更内容**: 盤面を in-place で変更し、探索後に元に戻す MakeMove/UnmakeMove パターンに移行する。

```csharp
// 着手情報を保持する構造体（スタック上に配置）
private struct MoveInfo
{
    public ulong PrevBlack;
    public ulong PrevWhite;
    public Disc.Color PrevTurn;
    public int PrevTurnCount;
    public int PrevStage;
    public int PrevMove;
    public ulong PrevMobility;
}

private MoveInfo MakeMove(GameContext context, int move)
{
    // 着手前の状態を保存
    var info = new MoveInfo
    {
        PrevBlack = context.Black,
        PrevWhite = context.White,
        PrevTurn = context.Turn,
        PrevTurnCount = context.TurnCount,
        PrevStage = context.Stage,
        PrevMove = context.Move,
        PrevMobility = context.Mobility,
    };

    // in-place で盤面を更新
    context.Move = move;
    _reverseUpdater.Update(context);
    BoardAccessor.NextTurn(context);

    return info;
}

private void UnmakeMove(GameContext context, MoveInfo info)
{
    // 盤面を元に戻す
    context.Black = info.PrevBlack;
    context.White = info.PrevWhite;
    context.Turn = info.PrevTurn;
    context.TurnCount = info.PrevTurnCount;
    context.Stage = info.PrevStage;
    context.Move = info.PrevMove;
    context.Mobility = info.PrevMobility;
}
```

呼び出し側（`Pvs` メソッド内）の変更:

```csharp
// 変更前
var child = MakeMove(context, move);
long score = -Pvs(child, remainingDepth - 1, -beta, -alpha, false);

// 変更後
var moveInfo = MakeMove(context, move);
long score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false);
UnmakeMove(context, moveInfo);
```

**[仮定]** `MoveAndReverseUpdater.Update` が `context` を in-place で変更する現行の実装が、UnmakeMove で完全に元に戻せることを前提とする。`MoveAndReverseUpdater.Update` のソースコードを確認した結果、`BoardAccessor.SetTurnDiscs` / `SetOppositeDiscs` で `Black` / `White` フィールドを直接書き換えており、ビットボードの `Black` / `White` を保存・復元すれば完全に元に戻せることを確認済みである。

**効果**: 探索ノードごとの `GameContext` + `BoardContext` のヒープアロケーションを完全に排除する。

#### 5.2.2 BoardContext を record struct に変更

**対象ファイル**: `Reluca/Contexts/BoardContext.cs`, `Reluca/Contexts/GameContext.cs`

現状、`BoardContext` は `record`（参照型）であり、`with` 式のたびにヒープにオブジェクトが生成される。

**変更内容**: `BoardContext` を `record struct`（値型）に変更する。

```csharp
// 変更後
public record struct BoardContext
{
    public ulong Black { get; set; }
    public ulong White { get; set; }
}
```

**注意**: `record struct` に変更すると、`GameContext` 内での `BoardContext` の扱いが値コピーになる。Phase 2 で MakeMove/UnmakeMove パターンを採用する場合、DeepCopy は使用しなくなるため、この変更との相性は良い。ただし、`BoardContext` を引数として渡している全箇所で、値渡し/参照渡しの挙動が変わるため、影響範囲の調査が必要である。

**影響範囲**: `BoardContext` を使用する全てのコンポーネント。特に以下の箇所で挙動変化の可能性がある:
- `BoardAccessor.DeepCopy(BoardContext)` — MakeMove/UnmakeMove 導入後は不要になる
- `FeaturePatternExtractor.Extract(BoardContext)` — 引数が値コピーになる
- `FeaturePatternEvaluator.Evaluate` — `context.Board` のアクセスが値コピーになる

**[仮定]** `BoardContext` はフィールドが `ulong` 2 つのみ（16 バイト）であり、値型としてスタックコピーするコストは十分に小さいと判断する。

#### 5.2.3 PVS/NegaScout の Null Window Search 実装

**対象ファイル**: `Reluca/Search/PvsSearchEngine.cs`

現状、`Pvs` メソッド内で全手をフルウィンドウ `(-beta, -alpha)` で探索している。PVS/NegaScout アルゴリズムでは、最初の手（Principal Variation）のみフルウィンドウで探索し、2 手目以降は Null Window `(-alpha-1, -alpha)` で探索する。Null Window Search で fail-high した場合のみフルウィンドウで再探索する。

**変更内容**:

```csharp
// Pvs メソッド内のメインループ変更
bool isFirstMove = true;
foreach (var move in moves)
{
    var moveInfo = MakeMove(context, move);
    long score;

    if (isFirstMove)
    {
        // 最初の手: フルウィンドウで探索
        score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false);
        isFirstMove = false;
    }
    else
    {
        // 2手目以降: Null Window Search
        score = -Pvs(context, remainingDepth - 1, -alpha - 1, -alpha, false);
        if (score > alpha && score < beta)
        {
            // fail-high: フルウィンドウで再探索
            score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false);
        }
    }

    UnmakeMove(context, moveInfo);

    if (score >= beta)
    {
        // ベータカット（TT Store 含む既存処理）
        // ...
        return score;
    }

    if (score > maxValue)
    {
        maxValue = score;
        localBestMove = move;
        alpha = Math.Max(alpha, maxValue);
    }
}
```

**効果**: 2 手目以降の探索コストを大幅に削減する。Move Ordering が適切であれば、Null Window Search の大半は fail-low（カット成功）となり、フルウィンドウ探索は最初の 1 手のみで済む。典型的には探索ノード数が 30〜50% 削減される。

#### 5.2.4 Aspiration 成功時の二重 RootSearch 排除

**対象ファイル**: `Reluca/Search/PvsSearchEngine.cs`

現状、`AspirationRootSearch` では Aspiration Window 内に収まった場合、TT Store 有効化のために同じ探索を 2 回実行している。

```csharp
// 現状（PvsSearchEngine.cs:386-401）
_suppressTTStore = true;
var result = RootSearch(context, depth, alpha, beta);
if (result.Value <= initialAlpha || result.Value >= initialBeta)
{
    // fail → retry
}
else
{
    _suppressTTStore = false;
    return RootSearch(context, depth, alpha, beta); // 2回目
}
```

**変更内容**: 1 回の RootSearch 内で TT Store の抑制/許可を制御する方式に変更する。具体的には、RootSearch 完了後に結果が窓内に収まっていれば、TT Store のみ後追いで実行する。

```csharp
private SearchResult AspirationRootSearch(GameContext context, int depth, long prevValue)
{
    // ... delta 計算は既存と同じ ...

    int retryCount = 0;
    while (retryCount <= _options.AspirationMaxRetry)
    {
        long initialAlpha = prevValue - delta;
        long initialBeta = prevValue + delta;
        long alpha = Math.Max(initialAlpha, DefaultAlpha);
        long beta = Math.Min(initialBeta, DefaultBeta);

        // TT Store を有効にして探索（1回のみ）
        _suppressTTStore = false;
        var result = RootSearch(context, depth, alpha, beta);

        if (result.Value <= initialAlpha || result.Value >= initialBeta)
        {
            // fail: delta を拡張して再探索
            delta = ExpandDelta(delta, retryCount, useExponentialExpansion);
            retryCount++;
            _aspirationRetryCount++;
        }
        else
        {
            // 成功: 結果をそのまま返す（TT Store は RootSearch 内で完了済み）
            return result;
        }
    }

    // フォールバック
    _aspirationFallbackCount++;
    _suppressTTStore = false;
    return RootSearch(context, depth, DefaultAlpha, DefaultBeta);
}
```

**[仮定]** 現行の `_suppressTTStore = true` で 1 回目を探索する設計は、fail 時に不正確な TT エントリが書き込まれることを防ぐ意図であると推測する。しかし、Aspiration Window の失敗は窓が狭すぎることが原因であり、探索結果自体は正しい（BoundType が UpperBound / LowerBound として正しく記録される）。したがって、TT Store を抑制せずに探索しても置換表の正しさは損なわれない。fail 時に書き込まれたエントリは、再探索のより広い窓で上書きされるか、BoundType により適切にフィルタリングされる。

**効果**: Aspiration 成功時の探索時間を半減させる。反復深化の各深さで 1 回の RootSearch で済むようになる。

## 6. 代替案の検討 (Alternatives Considered)

### 案A: Phase 1 のみ実施し Phase 2 は保留

- **概要**: 変更範囲が限定的な Phase 1 の 4 項目のみを実施し、Phase 2 の構造的変更は計測結果を見てから判断する。
- **長所**: 変更リスクが低い。既存のアーキテクチャを維持できる。段階的に効果を検証できる。
- **短所**: GC オブジェクト生成の根本原因（MakeMove の DeepCopy）が残るため、改善効果に限界がある。Null Window Search 未実装のまま探索ノード数が減らない。

### 案B: Phase 1 + Phase 2 を一括実施（本提案）

- **概要**: Phase 1 と Phase 2 を順次実施する。Phase 1 で即効性のある改善を先行し、Phase 2 で構造的な改善を追加する。
- **長所**: GC オブジェクト生成を実質ゼロに近づけ、Null Window Search による枝刈り効率改善も実現する。根本的な性能改善が得られる。
- **短所**: Phase 2 の変更範囲が広く、特に MakeMove/UnmakeMove パターンへの移行と `record struct` 化は影響範囲が大きい。テスト工数も増加する。

### 案C: GameContext/BoardContext を class に変更し Object Pool で管理

- **概要**: `record` のままではなく通常の `class` に変更し、`ObjectPool<GameContext>` でプーリングすることで GC 圧力を軽減する。
- **長所**: MakeMove/UnmakeMove パターンのような根本的な設計変更なしに GC 圧力を軽減できる。
- **短所**: Object Pool の管理コスト（取得・返却・クリア）が発生する。Pool サイズの調整が必要。返却忘れによるメモリリークのリスクがある。MakeMove/UnmakeMove パターンほどのゼロアロケーションは実現できない。

### 選定理由

案 B を採用する。理由は以下の通りである:

1. Phase 1 は変更範囲が限定的であり即座に実施可能である。Phase 2 は Phase 1 の効果測定後に着手することでリスクを管理できる。
2. MakeMove/UnmakeMove パターンはオセロ・チェス等のゲーム探索エンジンにおける標準的な手法であり、実績が豊富である。Object Pool（案 C）と比較して、ゼロアロケーションを達成でき管理コストも不要である。
3. Null Window Search の実装は PVS アルゴリズムの本来の姿であり、名称と実装の乖離を解消するという正当性もある。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

- 本改善はパフォーマンス最適化であり、セキュリティ・プライバシーへの影響はない。

### 7.2 スケーラビリティとパフォーマンス

- **GC 圧力の削減**: Phase 1 で 1 ノードあたりの GC オブジェクト生成を約 15 個削減（List アロケーション排除 + LINQ 排除）。Phase 2 で残りの DeepCopy 由来のアロケーションを排除し、実質ゼロアロケーションを実現する。
- **探索ノード数の削減**: Null Window Search の実装により、同一深さでの探索ノード数を 30〜50% 削減する。
- **時間効率の改善**: Aspiration 二重探索の排除により、反復深化の各深さの所要時間を最大 50% 短縮する。
- **計測方法**: `dotnet-counters` で GC 回数・Gen0/Gen1 コレクション回数を計測する。`Stopwatch` による探索時間の計測は既存の仕組みで対応可能である。

### 7.3 可観測性 (Observability)

- 既存のログ出力（`_logger.LogInformation` による探索進捗ログ）は維持する。
- Phase 2 の Null Window Search 実装後、re-search 発生回数をログに追加することを検討する。

### 7.4 マイグレーションと後方互換性

- **API 互換性**: `ISearchEngine.Search` のインターフェースは変更しない。外部からの呼び出し方法に変更はない。
- **内部互換性**: `FeaturePatternExtractor.Extract` のシグネチャが変更される（`ExtractNoAlloc` の追加）。既存の `Extract` メソッドは残し、新メソッドを追加する形とする。
- **`BoardContext` の `record struct` 化**: 参照型から値型への変更は破壊的変更である。ただし `BoardContext` はライブラリ内部でのみ使用されており、外部 API の一部ではないため、外部互換性への影響はない。
- **探索結果の変化**: Null Window Search の実装により、同一局面・同一深さでの探索結果（最善手・評価値）は変わらないが、探索パス（訪問ノードの順序）は変化する。

## 8. テスト戦略 (Test Strategy)

- **Phase 1 検証**:
  - 既存の単体テスト（`Reluca.Tests`）が全て通ることを確認する
  - 特定の局面に対して Phase 1 適用前後で探索結果（最善手・評価値）が同一であることを検証する
  - `dotnet-counters` で GC 回数の削減を定量的に確認する
- **Phase 2 検証**:
  - MakeMove/UnmakeMove パターン: 探索前後で盤面状態が完全に復元されることを検証する単体テストを追加する
  - `record struct` 化: 既存テストの通過に加え、値コピーの挙動が正しいことを確認する
  - Null Window Search: 複数の局面で探索結果がフルウィンドウ探索と同一であることを検証する。特に、re-search が正しくトリガーされるケースを含める
  - Aspiration 二重探索排除: 既存の Aspiration テスト（fail-low/high/success の各パターン）が通ることを確認する
- **回帰テスト**:
  - Windows Forms UI での実対局を Phase 1・Phase 2 それぞれの完了後に実施し、体感速度の改善を確認する

## 9. 実装・リリース計画 (Implementation Plan)

### Phase 1（即効性のある改善）

| ステップ | 内容 | 成果物 |
|---------|------|--------|
| 1-1 | DI コンテナ呼び出し排除 | `MobilityAnalyzer` のコンストラクタインジェクション化 |
| 1-2 | `AnalyzeCount` メソッド追加 | `MobilityAnalyzer.AnalyzeCount`, `FeaturePatternEvaluator` の呼び出し変更 |
| 1-3 | OrderMoves の LINQ 排除 | `PvsSearchEngine.OrderMoves` の書き換え |
| 1-4 | `FeaturePatternExtractor.ExtractNoAlloc` 追加 | 事前確保配列方式への変更 |
| 1-5 | GC 計測・効果検証 | `dotnet-counters` による計測結果レポート |

### Phase 2（構造的な改善）

| ステップ | 内容 | 成果物 |
|---------|------|--------|
| 2-1 | MakeMove/UnmakeMove パターン実装 | `PvsSearchEngine` の MakeMove/UnmakeMove 化 |
| 2-2 | `BoardContext` の `record struct` 化 | 値型化 + 影響範囲の修正 |
| 2-3 | Null Window Search 実装 | `Pvs` メソッドの NWS 対応 |
| 2-4 | Aspiration 二重探索排除 | `AspirationRootSearch` の単一探索化 |
| 2-5 | 統合テスト・効果検証 | 実対局での速度検証、GC 計測 |

### リスク軽減策

- Phase 1 と Phase 2 は独立してリリース可能である。Phase 1 の効果が十分であれば Phase 2 の優先度を調整できる。
- 各ステップ完了ごとに既存テストの通過を確認する。
- Phase 2 の MakeMove/UnmakeMove パターン導入時は、既存の DeepCopy 方式を残しつつ新方式を並行実装し、結果の一致を検証してから切り替える。

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: Phase 2 完了後に以下を更新する必要がある:
  - `BoardContext` の `record struct` 化に伴う「データ層」セクションの記述更新
  - `MobilityAnalyzer` のコンストラクタインジェクション化に伴う「依存性注入」セクションの更新（`DiProvider.Get()` 使用例の見直し）
- **`docs/domain-model.md`**: Phase 2 完了後に以下を更新する必要がある:
  - `BoardContext` の型定義（`record` → `record struct`）の記述更新
- **`docs/api-overview.md`**: 存在しないため影響なし
