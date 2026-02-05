# [RFC] 探索ノード数計測の導入

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Accepted (承認済) |
| **作成日** | 2026-02-05 |
| **タグ** | Search, 最優先 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md) |

## 1. 要約 (Summary)

- PVS 探索エンジンに探索ノード数（NodesSearched）の計測機能を導入する。
- `SearchResult` に `NodesSearched` プロパティを追加し、探索完了時にノード数を返却する。
- `PvsSearchEngine` の各ノード展開時にカウントを加算し、TT Probe ヒット時のカウント処理も行う。
- 反復深化の各深さごとの NodesSearched を `Console.WriteLine` で標準出力に出力する。
- 本 RFC は後続の RFC 2〜4（Multi-ProbCut、Aspiration Window チューニング、時間制御）の前提となる計測基盤である。

## 2. 背景・動機 (Motivation)

- 現在の Reluca 探索エンジンには、探索効率を定量的に評価する手段が存在しない。`SearchResult` は `BestMove` と `Value` のみを保持しており、探索にどれだけのノードを展開したかを知ることができない。
- 後続の RFC（Multi-ProbCut、Aspiration Window チューニング、時間制御）では、いずれも「探索効率の改善」を主指標として評価する必要がある。計測基盤がなければ、これらの改善効果を定量的に検証できない。
- WZebra のアルゴリズム組み込みロードマップにおいて、NodesSearched 計測は全 RFC の前提条件として位置付けられている。
- 放置した場合、TT の ON/OFF や Aspiration Window の効果を数値で示すことができず、パラメータ調整が経験則に頼ることになる。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- 探索完了時に NodesSearched が正の値として返されること
- TT ON/OFF で NodesSearched の差分が確認できること（TT ON 時に減少）
- 反復深化の各深さごとの NodesSearched を標準出力で確認できること
- 既存の探索結果（BestMove、Value）に一切影響を与えないこと

### やらないこと (Non-Goals)

- NPS（Nodes Per Second）の計測。時間計測は RFC 4（time-limit-search）のスコープとする
- LegacySearchEngine（CachedNegaMax）への NodesSearched 導入。PvsSearchEngine のみを対象とする
- UI への NodesSearched 表示
- 構造化ログ基盤（Logger クラス）の導入。ログ機構の刷新は独立した横断的関心事であり、別 RFC として切り出す

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- Task 3d.2（安定性修正）まで完了していること（完了済み）
- PvsSearchEngine が ISearchEngine インターフェースを実装していること（実装済み）
- 置換表（ZobristTranspositionTable）が統合済みであること（統合済み）

## 5. 詳細設計 (Detailed Design)

### 5.1 SearchResult への NodesSearched 追加

`Reluca/Search/SearchResult.cs` に `NodesSearched` プロパティを追加する。

```csharp
/// <summary>
/// 探索結果を保持する
/// </summary>
public class SearchResult
{
    /// <summary>
    /// 最善手
    /// </summary>
    public int BestMove { get; }

    /// <summary>
    /// 評価値
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// 探索ノード数
    /// </summary>
    public long NodesSearched { get; }

    /// <summary>
    /// SearchResult を生成する
    /// </summary>
    /// <param name="bestMove">最善手</param>
    /// <param name="value">評価値</param>
    /// <param name="nodesSearched">探索ノード数</param>
    public SearchResult(int bestMove, long value, long nodesSearched = 0)
    {
        BestMove = bestMove;
        Value = value;
        NodesSearched = nodesSearched;
    }
}
```

`nodesSearched` パラメータにはデフォルト値 `0` を設定する。これにより `LegacySearchEngine` など NodesSearched を計測しない呼び出し元は変更不要となる。

### 5.2 PvsSearchEngine へのカウンタ導入

`Reluca/Search/PvsSearchEngine.cs` にインスタンスフィールドとしてカウンタを追加する。

```csharp
/// <summary>
/// 探索ノード数カウンタ
/// </summary>
private long _nodesSearched;
```

#### カウント方針

NodesSearched のカウント定義は「`Pvs()` メソッドの呼び出し回数」とする。`Pvs()` が呼び出されるたびに 1 ノードとしてカウントする。これには葉ノード（`remainingDepth == 0` で評価関数を呼ぶノード）、TT ヒットで早期リターンするノード、パスノードの再帰呼び出しがすべて含まれる。

インクリメント箇所は `Pvs()` メソッド先頭の 1 箇所のみである。

| 箇所 | タイミング | 理由 |
|------|-----------|------|
| `Pvs()` メソッド先頭 | メソッド呼び出し時 | 再帰呼び出しされた全ノード（葉ノード・TT ヒットノード・パスノードを含む）をカウントする |

`RootSearch()` のループ内で `Pvs()` を呼ぶ際、ルートの各子ノードは `Pvs()` メソッド先頭のインクリメントによりカウントされる。`RootSearch()` 側に追加のカウント処理は不要である。

**[仮定]** ルート局面自体は 1 ノードとしてカウントしない。ルート局面は合法手の列挙のみを行い、`Pvs()` を経由しないためである。`RootSearch()` 内で合法手が 0 の場合に `Evaluate(context)` を直接呼び出すケースも存在するが、これはゲーム終了に近い例外的な状況であり、`Pvs()` を経由しないためカウント対象外とする。本 RFC での NodesSearched は Reluca 内部の探索効率評価を目的としており、外部エンジン（Stockfish 等）との直接的な NodesSearched 比較は想定しない。

#### 葉ノードの扱い

葉ノード（`remainingDepth == 0` または終局で `Evaluate` を呼ぶノード）はカウント対象に含む。既存の `Pvs()` メソッドでは終了条件チェックがメソッド先頭にあるため、`_nodesSearched++` は終了条件チェックの前に挿入する。これにより既存の制御フロー（終了条件 → TT Probe → 合法手展開）を変更せずにカウントを追加できる。

#### TT Probe ヒット時の扱い

TT Probe ヒット時は `Pvs()` メソッドの先頭でカウント済みであるため、追加のカウント処理は不要である。TT Probe によるカットオフは「探索したが早期に打ち切れた」ノードとして 1 ノードにカウントされる。

これにより TT ON 時は「TT ヒットで打ち切られたノードは 1 としてカウントされるが、その先の子孫ノードは展開されないため NodesSearched が減少する」という挙動となり、TT の効果を定量的に評価できる。

#### パスノードの扱い

合法手が 0 の場合のパス処理（`BoardAccessor.Pass` → `Pvs()` 再帰呼び出し）では、再帰呼び出し先の `Pvs()` メソッド先頭でカウントされる。パスノードはカウント対象に含む。パスは新しい着手を伴わないが、`Pvs()` の呼び出しとして探索木の一部を構成し、終了条件の評価や子ノードの展開が発生するためである。

#### Aspiration Window 再探索時のカウント方針

現在の `AspirationRootSearch` は、以下の流れで `RootSearch` を複数回呼び出す。

1. **retry 探索**（`_suppressTTStore = true`）: 狭い窓で探索し、fail-low/fail-high の場合は窓を拡大して再試行する（最大 `AspirationMaxRetry` 回）
2. **確定探索**（`_suppressTTStore = false`）: 窓内に収まった場合、TT Store を許可して同一窓で再探索する
3. **フォールバック探索**（`_suppressTTStore = false`）: retry 上限超過時、フルウィンドウで探索する

`_nodesSearched` は反復深化の各深さの開始時に 1 回リセットし、以降はリセットしない。`AspirationRootSearch` 内の全ての `RootSearch` 呼び出し（retry・確定・フォールバック）で展開されたノードは累積される。

**[仮定]** retry 探索および確定探索の再探索で展開されたノードも含めてカウントする。これらは実際に CPU 時間を消費する探索処理であり、探索コストの一部である。Aspiration Window の窓幅やリトライ戦略の効果を評価する際にも、全てのノードを含めた方が正確な比較が可能である。

#### 実装の詳細

**`Search()` メソッド（反復深化ループ）:**

```csharp
public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
{
    // ... 既存の初期化処理 ...

    long totalNodesSearched = 0;

    for (int depth = 1; depth <= options.MaxDepth; depth++)
    {
        _nodesSearched = 0;  // 深さごとにリセット（AspirationRootSearch 内の全 RootSearch 呼び出しを累積）

        // ... 既存の探索処理（AspirationRootSearch or RootSearch）...

        totalNodesSearched += _nodesSearched;

        // 深さごとのノード数を標準出力に出力
        Console.WriteLine($"depth={depth} nodes={_nodesSearched} total={totalNodesSearched} value={result.Value}");
    }

    return new SearchResult(bestMoveResult, maxValueResult, totalNodesSearched);
}
```

`_nodesSearched` のリセットは `for` ループの先頭で 1 回のみ行う。`AspirationRootSearch` 内で `RootSearch` が複数回呼び出されても、`_nodesSearched` はリセットされず累積される。これにより、当該深さで実際に展開された全ノード数が `_nodesSearched` に反映される。

**`Pvs()` メソッド先頭（既存の制御フローに合わせた挿入位置）:**

既存の `Pvs()` メソッドの制御フローは「終了条件チェック → TT Probe → 合法手展開」の順である。`_nodesSearched++` はこの制御フローを変更せず、終了条件チェックの前に挿入する。

```csharp
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
{
    _nodesSearched++;  // ノードカウント（葉ノード・TT ヒットノード・パスノードを含む全ノード）

    // 終了条件: 残り深さ 0 または終局（既存の制御フローを維持）
    if (remainingDepth == 0 || BoardAccessor.IsGameEndTurnCount(context))
    {
        return Evaluate(context);
    }

    // TT Probe（ヒット時は早期リターン、カウント済み）
    // ... 既存処理 ...

    // 合法手展開 or パス処理
    // ... 既存処理 ...
}
```

### 5.3 探索ログの出力

反復深化の各深さ完了時に、以下の情報を `Console.WriteLine` で標準出力に出力する。

| フィールド | 説明 |
|-----------|------|
| `depth` | 探索深さ |
| `nodes` | 当該深さでの探索ノード数（Aspiration retry 分を含む） |
| `total` | 累計探索ノード数 |
| `value` | 当該深さでの評価値 |

```csharp
Console.WriteLine($"depth={depth} nodes={_nodesSearched} total={totalNodesSearched} value={result.Value}");
```

ログ出力には既存の `Console.WriteLine` を使用する。構造化ログ基盤（Logger クラス）の導入は別 RFC のスコープとし、本 RFC では最小限の標準出力で計測結果を確認できることを目的とする。後続の Logger RFC では、ログレベル制御、テスト時の出力抑制、ビルド構成（Debug/Release）に応じた出力制御を設計に含めることを推奨する。

### 5.4 ISearchEngine インターフェースへの影響

`ISearchEngine` インターフェースの `Search()` メソッドシグネチャは変更しない。戻り値の `SearchResult` に `NodesSearched` が追加されるのみであり、`SearchResult` のコンストラクタにデフォルト値を設定することで後方互換性を維持する。

### 5.5 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Reluca/Search/SearchResult.cs` | `NodesSearched` プロパティ追加、コンストラクタ拡張 |
| `Reluca/Search/PvsSearchEngine.cs` | `_nodesSearched` フィールド追加、カウントロジック、`Console.WriteLine` によるログ出力 |

## 6. 代替案の検討 (Alternatives Considered)

### ノードカウントの実装方式

#### 案A: インスタンスフィールドによるカウント（採用案）

- **概要**: `PvsSearchEngine` にインスタンスフィールド `_nodesSearched` を持ち、探索メソッド内で直接インクリメントする。
- **長所**: 実装がシンプル。追加のオブジェクト生成や間接参照がなく、パフォーマンスへの影響が最小。既存の `_bestMove` や `_currentDepth` と同じパターンで一貫性がある。
- **短所**: `PvsSearchEngine` の責務が若干増える。スレッドセーフではないが、現状シングルスレッド探索のため問題にならない。

#### 案B: SearchStatistics クラスの導入

- **概要**: `SearchStatistics` クラスを新設し、NodesSearched をはじめとする各種統計情報（TT ヒット数、ベータカット数等）をまとめて管理する。
- **長所**: 将来的に統計情報を拡張しやすい。関心の分離が明確。
- **短所**: 現時点では NodesSearched のみが必要であり、クラス新設はオーバーエンジニアリングである。`PvsSearchEngine` から `SearchStatistics` への参照渡しが必要になり、再帰メソッドのシグネチャが変わるか、フィールドとして保持する場合は案 A と本質的に同じになる。

#### 選定理由

現時点で必要な計測指標は NodesSearched のみである。案 B は将来の拡張性で優れるが、YAGNI の原則に基づき、必要最小限の変更で目的を達成できる案 A を採用する。将来 TT ヒット率やベータカット率の計測が必要になった時点で、案 B へのリファクタリングを検討すればよい。

### ログ出力の方式

#### 案C: 構造化ログ基盤（Logger クラス）の導入

- **概要**: JSON Lines 形式の構造化ログ、ログレベル、日付ベースローテーション、自動クリーンアップを備えた独自 Logger クラスを本 RFC で併せて導入する。
- **長所**: 後続 RFC で構造化ログを即座に活用できる。可観測性の基盤が早期に整う。
- **短所**: 本 RFC の本質的な課題（探索ノード数の計測基盤がないこと）とは独立した横断的関心事であり、1 つの RFC に 2 つの課題を混在させることで、レビュー・実装・ロールバックの単位が不明確になる。Logger 単体で設計上の論点（テスト容易性、スレッドセーフティ、排他制御等）が多く、計測機能の導入を遅延させるリスクがある。

#### 案D: ログ出力を行わない（SearchResult のみ）

- **概要**: `SearchResult.NodesSearched` のみを返却し、ログ出力は一切行わない。ログ出力は呼び出し元の責務とする。
- **長所**: 変更範囲が最小。探索エンジンの責務が純粋に保たれる。
- **短所**: 反復深化の各深さごとの NodesSearched を確認する手段がない。呼び出し元は最終結果の合計ノード数しか参照できず、深さごとの探索効率分析ができない。後続 RFC でのパラメータ調整時に、深さ別の情報が必要となる場面が確実に存在する。

#### 案E: Console.WriteLine による最小限の出力（採用案）

- **概要**: 既存の `Console.WriteLine` を使用し、反復深化の各深さ完了時にノード数と評価値を標準出力に出力する。
- **長所**: 追加の依存関係や新規クラスが不要。深さごとの NodesSearched を即座に確認可能。後続 RFC で Logger クラスが導入された際に、`Console.WriteLine` を `Logger.Info()` に置き換えるだけで移行できる。
- **短所**: ファイル永続化されない。構造化されていないため自動解析には不向き。

#### 選定理由（ログ出力）

本 RFC の目的は「探索ノード数の計測基盤を導入すること」であり、ログ機構の刷新ではない。案 D はログ出力を一切行わないため深さごとの分析ができず不十分である。案 C は独立した横断的関心事を混在させるためスコープが過大である。案 E は最小限の変更で深さごとの NodesSearched を確認でき、後続の Logger RFC への移行も容易であるため採用する。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

- `_nodesSearched++` は `long` 型の単純なインクリメントであり、探索ノードあたりのオーバーヘッドは無視できる水準である。
- ログ出力は反復深化の各深さ完了時（最大 MaxDepth 回）のみであり、ノード展開ごとではないためパフォーマンスに影響しない。
- `SearchResult` への `NodesSearched` 追加は `long` 型フィールド 1 つであり、メモリへの影響は無視できる。

### 7.2 スレッドセーフティ

- 本 RFC の `_nodesSearched` インクリメントはシングルスレッド実行を前提としており、排他制御は行わない。現在の `PvsSearchEngine` はシングルスレッドで動作するため問題にならない。
- 将来マルチスレッド探索（Lazy SMP 等）を導入する場合は、`_nodesSearched` のインクリメントを `Interlocked.Increment` に置き換える必要がある。

### 7.3 マイグレーションと後方互換性

- `SearchResult` のコンストラクタにデフォルト値（`nodesSearched = 0`）を設定するため、既存の呼び出し元（`LegacySearchEngine` 等）は変更不要である。
- `ISearchEngine` インターフェースの変更はない。
- 破壊的変更はない。

## 8. テスト戦略 (Test Strategy)

テストは既存のテストフレームワーク（MSTest）を使用して記述する。

### ユニットテスト

| テスト観点 | 内容 |
|-----------|------|
| 正の値の検証 | 探索完了時に `NodesSearched > 0` であること |
| TT 効果の検証 | 同一局面で TT ON 時の NodesSearched が TT OFF 時より少ないこと |
| 探索結果の非干渉 | NodesSearched 導入前後で `BestMove` と `Value` が一致すること |
| 深さごとの累積 | 反復深化の全深さの NodesSearched 合計が `SearchResult.NodesSearched` と一致すること |

### Console.WriteLine のテスト時の扱い

テスト時の `Console.WriteLine` による標準出力はテスト結果に影響しないため、特別な対応は行わない。「深さごとの累積」の検証は `SearchResult.NodesSearched`（合計値）を検証対象とし、標準出力のパースは行わない。後続の Logger RFC でログ出力の制御（ログレベルやテスト時の抑制）を検討する。

### 検証方法

- 既存テストを全て実行し、探索結果に変更がないことを確認する。
- テスト用の固定局面で TT ON/OFF それぞれの NodesSearched を取得し、TT ON 時に減少していることを検証する。

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: SearchResult 拡張

- `SearchResult` に `NodesSearched` プロパティを追加する
- コンストラクタにデフォルト値付きパラメータを追加する
- 既存テストが通過することを確認する

### フェーズ 2: カウンタ実装とログ出力

- `PvsSearchEngine` に `_nodesSearched` フィールドを追加する
- `Pvs()` メソッド先頭でのカウント処理を追加する
- `Search()` メソッドで深さごとのリセットと累計を実装する
- `SearchResult` 生成時に `totalNodesSearched` を渡す
- 反復深化の各深さ完了時に `Console.WriteLine` でノード数を出力する

### フェーズ 3: テストと検証

- NodesSearched が正の値であることを検証するテストを追加する
- TT ON/OFF での NodesSearched 差分を検証するテストを追加する
- 既存テストが全て通過することを確認する

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響なし。新規クラスの追加はない。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータモデルに変更はない。
- `docs/api-overview.md`: 存在しない（対象外）。
