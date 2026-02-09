# [RFC] 終盤探索フリーズの修正

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | Claude Opus 4.6 |
| **ステータス** | Accepted (承認済) |
| **作成日** | 2026-02-09 |
| **タグ** | Search, Movers, Performance, P0 |
| **関連リンク** | `docs/rfcs/20260209-search-engine-phase3-speedup/rfc.md` |

## 1. 要約 (Summary)

Windows 環境で Reluca の対局を行うと、終盤読み（ターン46以降）で画面が長時間固まる問題が発生している。原因は3つある。(1) 反復深化の上限が `EndgameDepth = 99` に設定されており、残り空きマス数を超えた不要な深さまで探索を試みる。(2) 連続パス（両者合法手なし = ゲーム終了）の検出が探索木内に存在せず、`remainingDepth` が0になるまで不要な探索が続く。(3) 終盤では `DiscCountEvaluator` が使われるにもかかわらず、パターンインデックスの差分更新が毎ノードで実行される。本 RFC では探索エンジン側の3つの改善により、終盤読みの不要な計算を排除し、フリーズ問題を解消する。

## 2. 背景・動機 (Motivation)

### 現状の問題

Reluca の Windows Forms UI（`Reluca.Ui.WinForms/BoardForm.cs:196`）では、CPU 着手処理が UI スレッド上で同期実行される。終盤（ターン46以降）に入ると `FindBestMover` が以下の設定で探索を開始する:

```csharp
// FindBestMover.cs:33
private const int EndgameDepth = 99;

// FindBestMover.cs:70-76
if (context.TurnCount >= EndgameTurnThreshold)
{
    evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
    depth = EndgameDepth;
    // 終盤完全読み切りモードでは時間制限を適用しない
}
```

`timeLimitMs = null` で時間制限がないため、反復深化は depth=1 から depth=99 まで走ろうとする。

### 不要な探索が発生するメカニズム

1. **反復深化の上限過剰**: ターン46時点で空きマスは最大 `64 - 46 - 4 = 14` 個である。depth=14 で完全読み切りが可能であるが、depth=15〜99 の85回分の反復深化ループが不要に実行される。各深さで合法手が0になるため即座に返るものの、反復深化ループ自体のオーバーヘッドが積み重なる。

2. **連続パス未検出**: `PvsSearchEngine.Pvs` メソッドのパス処理（L942-958）では、直前がパスだったかどうかを `isPassed` 引数で追跡しているが、連続パス（ゲーム終了）時にリーフ評価に落ちる分岐がない。代わりに `remainingDepth` が 0 になるまで再帰が続く。

3. **不要なパターン差分更新**: `PvsSearchEngine.Search` メソッド（L390-392）では、探索開始時にパターンインデックスのフルスキャンと `IncrementalMode = true` の設定が無条件に行われる。終盤で `DiscCountEvaluator` が使われる場合、パターンインデックスは参照されないため、`MakeMove` 内の差分更新処理（L1110-1138）は完全に不要である。

### 放置した場合のリスク

- ユーザがゲームをプレイする際に、終盤で数秒〜数十秒の応答停止が発生し、操作不能となる。
- ゲーム体験が著しく損なわれ、実用に耐えない。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

| # | Goal | 達成確認方法 (Verification) |
|---|------|-----------------------------|
| G1 | 反復深化の上限を残り空きマス数に制限し、不要な深さの探索を排除する | 終盤局面（ターン46以降、空きマス数が既知の局面）を `FindBestMover.Move` に渡し、`SearchResult.CompletedDepth` が `64 - PopCount(black \| white)` 以下であることを単体テストで検証する |
| G2 | 連続パス時にリーフ評価へ即座に遷移し、不要な再帰探索を排除する | 連続パス局面を含む終盤テストケースを作成し、連続パス検出後に `Evaluate` が呼ばれることを単体テストで検証する |
| G3 | 終盤探索でパターンインデックスの差分更新をスキップし、不要な計算を排除する | `DiscCountEvaluator` を評価関数として渡した場合に `_usePatternIncremental` が `false` のまま維持され、パターン差分更新が実行されないことを単体テストで検証する（検証方法の詳細はセクション8を参照） |

### やらないこと (Non-Goals)

- **UI 非同期化**: `BoardForm.cs` の CPU 着手処理を `Task.Run` でバックグラウンド実行に移す対策は、影響範囲が大きいため別途 RFC で対応する。
- **終盤専用の探索アルゴリズム**: 終盤に特化した MPC パラメータチューニングや終盤専用の枝刈り手法の導入は本 RFC のスコープ外とする。

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- .NET 8.0 環境での動作を前提とする。
- `System.Numerics.BitOperations.PopCount` が使用可能であること（.NET Core 3.0 以降で利用可能）。
- Phase 3 最適化（`feature/20260209-search-engine-phase3-speedup` ブランチ）がマージ済みであること。特にビットボード合法手生成、Zobrist 差分更新、パターンインデックス差分更新の実装に依存する。

## 5. 詳細設計 (Detailed Design)

### 5.1 対策1: 反復深化の上限を残り空きマス数に制限する

**変更対象**: `Reluca/Movers/FindBestMover.cs`

現在の `EndgameDepth = 99` を、残り空きマス数に動的に設定する。

**変更前**:
```csharp
// FindBestMover.cs:33
private const int EndgameDepth = 99;

// FindBestMover.cs:70-76
if (context.TurnCount >= EndgameTurnThreshold)
{
    evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
    depth = EndgameDepth;
}
```

**変更後**:
```csharp
if (context.TurnCount >= EndgameTurnThreshold)
{
    evaluator = DiProvider.Get().GetService<DiscCountEvaluator>();
    int emptyCount = 64 - BitOperations.PopCount(context.Black | context.White);
    depth = emptyCount;
}
```

**理由**: オセロの終盤では、空きマス数が探索の理論上の最大深さとなる。depth > emptyCount の反復深化は、探索木の全ノードで `remainingDepth == 0` に到達し、即座にリーフ評価に落ちるため完全に冗長である。この変更により、ターン46時点で最大85回の不要な反復深化ループが排除される。

**補足**: `EndgameDepth` 定数は不要となるため削除する。`System.Numerics.BitOperations` の `using` ディレクティブを追加する。`context.Black | context.White` の PopCount には初期配置の4石も含まれるため、`64 - PopCount(black | white)` は常に正確な空きマス数を返す。セクション2の `64 - 46 - 4 = 14` は TurnCount と初期配置石数を個別に考慮した説明であり、コード上の計算式 `64 - PopCount(black | white)` と数学的に等価である。

### 5.2 対策2: 連続パスの終了判定を追加する

**変更対象**: `Reluca/Search/PvsSearchEngine.cs` の `Pvs` メソッド

`Pvs` メソッドのパス処理ブロック（L942-958）に連続パス検出を追加する。

**パス処理を含むメソッド・パスの網羅的分析**:

`PvsSearchEngine` 内でパス処理（`isPassed` パラメータの伝播）を行うパスを以下に整理する。

1. **`RootSearch` メソッド（L666）**: ルート局面の合法手が0件の場合、`Evaluate` を呼んで即座に返す（L673-676）。ルートでは `isPassed` を管理しておらず、ルート局面が合法手なしの場合は探索自体が成立しないため、連続パス検出は不要である。
2. **`Pvs` メソッド（L808）**: 合法手が0件の場合のパス処理（L942-958）が唯一のパス伝播箇所である。`isPassed = true` を渡して再帰呼び出しを行う。本対策の修正対象である。
3. **`TryMultiProbCut` メソッド（L981）**: 浅い探索として `Pvs` を `isPassed: false` で呼び出す（L1014）。MPC の浅い探索ではパス状態を引き継がないため、連続パス検出の修正は不要である。
4. **`Pvs` 内の PVS 再探索パス（L906-911）**: Null Window Search で fail-high した場合のフルウィンドウ再探索は、`MakeMove` で着手済みの子局面に対して行われるため `isPassed: false` で呼び出される。連続パス検出の修正は不要である。

以上の分析により、連続パス検出の修正が必要な箇所は `Pvs` メソッドのパス処理ブロック（L942-958）のみである。

**変更前**:
```csharp
else
{
    // パス処理: Turn を反転して再帰探索し、戻った後に Turn を復元する。
    var prevTurn = context.Turn;
    BoardAccessor.Pass(context);
    ulong passHash = currentHash ^ ZobristKeys.TurnKey;
    try
    {
        maxValue = -Pvs(context, remainingDepth - 1, -beta, -alpha, true, passHash);
    }
    finally
    {
        context.Turn = prevTurn;
    }
}
```

**変更後**:
```csharp
else
{
    if (isPassed)
    {
        // 連続パス = ゲーム終了 → リーフ評価を返す
        return Evaluate(context);
    }

    // パス処理: Turn を反転して再帰探索し、戻った後に Turn を復元する。
    var prevTurn = context.Turn;
    BoardAccessor.Pass(context);
    ulong passHash = currentHash ^ ZobristKeys.TurnKey;
    try
    {
        maxValue = -Pvs(context, remainingDepth - 1, -beta, -alpha, true, passHash);
    }
    finally
    {
        context.Turn = prevTurn;
    }
}
```

**理由**: オセロのルールでは、両プレイヤーが連続してパスした場合にゲーム終了となる。現在の実装では `isPassed == true` かつ `moves.Count == 0` の場合でも再帰探索を続行し、`remainingDepth` が 0 になるまで不要なパス処理が繰り返される。連続パス検出によりこの無駄な探索を即座に打ち切る。

**探索の正確性への影響**: 連続パスはゲーム終了を意味するため、それ以上の探索は不可能である。リーフ評価（石数差による評価）を返すのが正確な動作であり、探索結果の品質に悪影響はない。むしろ、現在の実装は `remainingDepth` 分だけパスを繰り返すという不正確な挙動を含んでいるため、本修正は正確性の向上でもある。

**`Evaluate` の符号の正当性**: `PvsSearchEngine.Evaluate` メソッド（L1059-1064）は `_evaluator.Evaluate(context)` の結果に `context.Turn` に応じたパリティ（黒番なら +1、白番なら -1）を乗じて返す。したがって、戻り値は常に現在の `context.Turn` 視点の評価値である。連続パス時は `context.Turn` が直前のパス処理で反転されていない（パス処理の `else` ブロック冒頭で `isPassed` を判定するため、`BoardAccessor.Pass` は呼ばれない）ため、現在の手番視点の正しい評価値が返される。NegaScout の符号規約とも整合する。

### 5.3 対策3: 終盤探索でパターン差分更新をスキップする

**変更対象**: `Reluca/Search/PvsSearchEngine.cs` の `Search` メソッド

パターンインデックスのフルスキャンと `IncrementalMode` 設定を、評価関数がパターンインデックスを必要とする場合にのみ実行するよう条件分岐を追加する。

**インターフェースの拡張**:

評価関数がパターンインデックスを必要とするかどうかの判定を、探索エンジンが具象型で行うのは開放閉鎖原則に違反する。`IEvaluable` インターフェースに `RequiresPatternIndex` プロパティを追加し、評価関数自身がパターンインデックスの必要性を宣言する設計とする。

**変更対象**: `Reluca/Evaluates/IEvaluable.cs`

```csharp
public interface IEvaluable
{
    /// <summary>
    /// ゲーム状態を評価します。
    /// </summary>
    /// <param name="context">ゲーム状態</param>
    /// <returns>ゲーム状態の評価値</returns>
    long Evaluate(GameContext context);

    /// <summary>
    /// パターンインデックスの差分更新を必要とするかどうかを示します。
    /// true の場合、探索エンジンは MakeMove/UnmakeMove 時にパターンインデックスの差分更新を実行します。
    /// </summary>
    bool RequiresPatternIndex { get; }
}
```

**各評価関数の実装**:

- `FeaturePatternEvaluator`: `public bool RequiresPatternIndex => true;`（パターンインデックスに依存する）
- `DiscCountEvaluator`: `public bool RequiresPatternIndex => false;`（石数差のみで評価し、パターンインデックスを参照しない）

将来新しい評価関数が追加された場合も、その評価関数自身が `RequiresPatternIndex` を宣言するため、`PvsSearchEngine` の修正は不要となる。

**変更前**:
```csharp
// Search メソッド内 (L390-392)
_featurePatternExtractor.ExtractNoAlloc(context.Board);
_featurePatternExtractor.IncrementalMode = true;
_patternChangeOffset = 0;
```

**変更後**:
```csharp
// 評価関数がパターンインデックスを必要とする場合のみ差分更新を有効化
_usePatternIncremental = evaluator.RequiresPatternIndex;
if (_usePatternIncremental)
{
    _featurePatternExtractor.ExtractNoAlloc(context.Board);
    _featurePatternExtractor.IncrementalMode = true;
    _patternChangeOffset = 0;
}
```

同様に、`Search` メソッドの終了時（L522）と `SearchTimeoutException` ハンドラ内（L480-483）の `IncrementalMode` 操作も `_usePatternIncremental` フラグで条件分岐する。

**`_usePatternIncremental` フラグの状態管理**:

`_usePatternIncremental` はインスタンスフィールドとして定義する。`Search` メソッドの冒頭で `evaluator.RequiresPatternIndex` に基づき設定し、`MakeMove`/`UnmakeMove` 内で参照する。

例外安全性を確保するため、`Search` メソッドの `finally` ブロックで `_usePatternIncremental = false` にリセットする。`SearchTimeoutException` ハンドラ内でも、パターン復元処理の後に `_usePatternIncremental = false` を実行する。これにより、例外発生時にフラグが不整合な状態で残ることを防止する。

```csharp
// Search メソッドの finally ブロック（L519-522 相当）
finally
{
    _stopwatch.Stop();
    if (_usePatternIncremental)
    {
        _featurePatternExtractor.IncrementalMode = false;
    }
    _usePatternIncremental = false;
}
```

```csharp
// SearchTimeoutException ハンドラ内（L480-483 相当）
catch (SearchTimeoutException)
{
    RestoreContext(context, rootBackup);

    if (_usePatternIncremental)
    {
        _featurePatternExtractor.IncrementalMode = false;
        _featurePatternExtractor.ExtractNoAlloc(context.Board);
        _featurePatternExtractor.IncrementalMode = true;
        _patternChangeOffset = 0;
    }
    _usePatternIncremental = false;

    // ... 以降のタイムアウト処理
}
```

**`MakeMove` / `UnmakeMove` への影響**:
`MakeMove` 内のパターン差分更新処理（L1110-1138）は `_featurePatternExtractor.GetSquarePatterns(square)` を呼び出してパターン逆引き情報を取得し、`_preallocatedResults` を更新する。`IncrementalMode == false` の場合、`ExtractNoAlloc` がフルスキャンを行うため差分更新は不要だが、`MakeMove` は `IncrementalMode` を参照せず常に差分更新を実行する。

したがって、`MakeMove` 内のパターン差分更新処理も `_usePatternIncremental` フラグで条件分岐する。

**変更箇所（`MakeMove`）**:
```csharp
// パターンインデックスの差分更新（パターンベース評価関数使用時のみ）
if (_usePatternIncremental)
{
    bool isBlackTurn = context.Turn == Disc.Color.Black;
    info.PatternChangeStart = _patternChangeOffset;
    int changeCount = 0;

    int moveDelta = isBlackTurn ? 1 : -1;
    changeCount += UpdatePatternIndicesForSquare(move, moveDelta);

    int flipDelta = isBlackTurn ? 2 : -2;
    ulong tmpFlipped = flipped;
    while (tmpFlipped != 0)
    {
        int sq = BitOperations.TrailingZeroCount(tmpFlipped);
        changeCount += UpdatePatternIndicesForSquare(sq, flipDelta);
        tmpFlipped &= tmpFlipped - 1;
    }
    info.PatternChangeCount = changeCount;
}
```

**変更箇所（`UnmakeMove`）**:
```csharp
// パターンインデックスの復元（パターンベース評価関数使用時のみ）
if (_usePatternIncremental)
{
    var results = _featurePatternExtractor.PreallocatedResults;
    int end = info.PatternChangeStart + info.PatternChangeCount;
    for (int i = end - 1; i >= info.PatternChangeStart; i--)
    {
        var change = _patternChangeBuffer[i];
        results[change.PatternType][change.SubPatternIndex] = change.PrevIndex;
    }
    _patternChangeOffset = info.PatternChangeStart;
}
```

**理由**: 終盤探索で `DiscCountEvaluator` が使用される場合、パターンインデックスは一切参照されない。`DiscCountEvaluator.Evaluate` は `BoardAccessor.GetDiscCount` のみを呼び出し、`FeaturePatternExtractor` の出力に依存しない。この不要な差分更新を省略することで、`MakeMove`/`UnmakeMove` のオーバーヘッドを削減する。

## 6. 代替案の検討 (Alternatives Considered)

### 案A: 探索エンジン側の最適化（本 RFC の採用案）

- **概要**: 対策1〜3 の3つの修正を探索エンジン内部に適用し、終盤探索の不要な計算を排除する。
- **長所**: 変更範囲が探索エンジンとその呼び出し元（`FindBestMover`）に限定される。UI 層への影響がなく、テストが容易である。即効性が高く、最小の変更で問題を解消できる。
- **短所**: UI スレッドでの同期実行という根本原因は解消されない。探索が高速化されても、将来的により深い読みや複雑な局面で再びフリーズする可能性がゼロではない。

### 案B: UI 非同期化（バックグラウンドスレッド）

- **概要**: `BoardForm.cs` の CPU 着手処理を `Task.Run` でバックグラウンド実行に移す。探索中も UI が応答し続ける。
- **長所**: 探索時間に関係なく UI が応答し続けるため、根本的な解決となる。「考え中...」表示などの UX 改善も可能になる。
- **短所**: `GameContext` のスレッドセーフティ確保が必要（現在はシングルスレッド前提で設計されている）。`PvsSearchEngine` のインスタンスフィールド群（`_bestMove`, `_nodesSearched` 等）の競合回避が必要。UI コントロールへのクロススレッドアクセス（`Invoke` / `BeginInvoke`）の対応が必要。影響範囲が広く、十分な検証期間を要する。

### 案C: 終盤に時間制限を設定する

- **概要**: `FindBestMover` で終盤にも `timeLimitMs` を設定し、一定時間で探索を打ち切る。
- **長所**: 実装が非常にシンプル（1行の変更）。
- **短所**: 完全読み切りが保証されなくなる。終盤の最善手の品質が低下する可能性がある。時間制限値の選定が難しい（局面の複雑さに依存する）。本来不要な計算を時間切れで中断するだけであり、根本的な効率化にはならない。

### 選定理由

案A を採用する。理由は以下の通りである:
1. 終盤の不要な計算を正確に特定・排除するため、探索品質を維持しつつフリーズを解消できる。
2. 変更範囲が限定的で、既存のテスト基盤で検証可能である。
3. 案B（UI 非同期化）は将来的に必要となる可能性があるが、本修正が先行することで緊急性が低下し、十分な設計・検証期間を確保できる。
4. 案C は探索品質を犠牲にするため、完全読み切りが可能な終盤では採用すべきでない。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 パフォーマンス

3つの対策はそれぞれ異なるレベルの削減効果を持つ。フリーズ解消への寄与度が大きい順に整理する。

**対策2（連続パス検出）が最大の寄与**: 連続パス未検出は、終盤の探索木を指数関数的に膨張させる主因である。連続パス状態に達した後も `remainingDepth` 分だけパス処理の再帰が続くため、ゲーム終了済みの局面に対して深さ分のノードが不要に展開される。終盤の探索木においてゲーム終了局面（両者合法手なし）は複数箇所に出現するため、累積的な無駄が非常に大きい。連続パス検出によりこれらの不要なノードが即座に打ち切られることで、探索ノード数が大幅に削減される。

**対策3（パターン差分更新スキップ）が次に寄与**: パターン差分更新は全ノードの `MakeMove`/`UnmakeMove` で実行されるため、探索ノード数に比例したオーバーヘッドとなる。1ノードあたりの削減量は小さいが、終盤探索では数十万〜数百万ノードが展開されるため、累積的な効果は大きい。

**対策1（反復深化上限制限）は補助的**: depth 超過分の反復深化ループは「合法手0件で即座に返る」ため、1ループあたりのコストは小さい。ただし85回分のループ排除により、探索開始時の不要なオーバーヘッドが確実に除去される。

これら3つの対策の相乗効果、特に対策2による探索木の適切な打ち切りにより、終盤探索の応答時間が大幅に改善され、UI フリーズ問題が解消される。

### 7.2 可観測性 (Observability)

既存のログ基盤（Serilog）を活用する。`PvsSearchEngine` の探索進捗ログ（depth, nodes, elapsed time）により、反復深化の上限が正しく制限されていることを確認できる。追加のログ出力は不要である。

### 7.3 後方互換性

探索エンジンのインターフェース（`ISearchEngine.Search`）に変更はない。`FindBestMover.Move` の戻り値（最善手のインデックス）にも変更はない。外部から観測可能な変更は、終盤の応答速度が改善されることのみである。

`IEvaluable` インターフェースに `RequiresPatternIndex` プロパティが追加されるため、`IEvaluable` を実装するすべてのクラスに本プロパティの実装が必要となる。現在の実装では `FeaturePatternEvaluator` と `DiscCountEvaluator` の2クラスのみが対象であり、影響範囲は限定的である。

`EndgameDepth` 定数の削除は、現在この定数が `FindBestMover` 内部でのみ使用されているため、外部への影響はない。

## 8. テスト戦略 (Test Strategy)

### 対策1: 反復深化上限の制限

- **単体テスト**: `FindBestMover.Move` に終盤局面（ターン46以降、空きマス数が既知の局面）を渡し、`SearchResult.CompletedDepth` が空きマス数以下であることを検証する。
- **境界値テスト**: ターン45（通常探索）とターン46（終盤探索）の境界で正しく切り替わることを検証する。

### 対策2: 連続パス検出

- **単体テスト**: 連続パス局面（両者とも合法手なし）を `PvsSearchEngine.Search` に渡し、`Evaluate` の結果（石数差に基づく値）が返ることを検証する。
- **エッジケース**: 片方のみパスの場合は従来通りパス処理が行われ、連続パスとして処理されないことを検証する。

### 対策3: パターン差分更新のスキップ

- **単体テスト**: `DiscCountEvaluator`（`RequiresPatternIndex = false`）を評価関数として渡した場合、探索結果が正しい石数差評価値を返すことを検証する。`FeaturePatternEvaluator` 使用時と同じ局面で、`DiscCountEvaluator` が返すべき評価値（石数差）と探索結果が一致することで、パターン差分更新のスキップが探索結果に悪影響を与えていないことを間接的に検証する。
- **回帰テスト**: `FeaturePatternEvaluator`（`RequiresPatternIndex = true`）を評価関数として渡した場合、差分更新が従来通り動作し、探索結果が変わらないことを検証する。

**デバッグカウンタについて**: 既存の `_debugPatternDeltaUpdateCount` は `#if DEBUG` 条件コンパイルで定義されており、テストプロジェクトは `DEBUG` 構成でビルドされる。ただし、テストからこのカウンタに直接アクセスするには `internal` アクセス修飾子 + `InternalsVisibleTo` が必要となり、テストのためだけにアクセシビリティを変更するのは過剰である。上述の通り、探索結果の値の正しさを検証する方式を主テスト手法とし、デバッグカウンタは開発時の手動確認用に留める。

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ1: 対策1（反復深化上限の制限）

- `FindBestMover.cs` の終盤探索深さを `EndgameDepth` から空きマス数に変更する。
- `EndgameDepth` 定数を削除する。
- 単体テストを追加する。

### フェーズ2: 対策2（連続パス検出）

- `PvsSearchEngine.Pvs` のパス処理に連続パス検出を追加する。
- 連続パス局面のテストケースを追加する。

### フェーズ3: 対策3（パターン差分更新のスキップ）

- `IEvaluable` インターフェースに `RequiresPatternIndex` プロパティを追加する。
- `FeaturePatternEvaluator` に `RequiresPatternIndex => true` を実装する。
- `DiscCountEvaluator` に `RequiresPatternIndex => false` を実装する。
- `PvsSearchEngine` に `_usePatternIncremental` フィールドを追加する。
- `Search`, `MakeMove`, `UnmakeMove` メソッドに条件分岐を追加する。
- `SearchTimeoutException` ハンドラおよび `finally` ブロックで `_usePatternIncremental` をリセットする処理を追加する。
- 単体テスト・回帰テストを追加する。

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響なし。コンポーネント構成やレイヤー構造に変更はない。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータ構造に変更はない。
- `docs/api-overview.md`: 該当ファイルなし。
