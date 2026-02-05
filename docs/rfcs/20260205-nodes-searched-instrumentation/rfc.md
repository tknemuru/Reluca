# [RFC] 探索ノード数計測の導入

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-05 |
| **タグ** | Search, 最優先 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md) |

## 1. 要約 (Summary)

- PVS 探索エンジンに探索ノード数（NodesSearched）の計測機能を導入する。
- `SearchResult` に `NodesSearched` プロパティを追加し、探索完了時にノード数を返却する。
- `PvsSearchEngine` の各ノード展開時にカウントを加算し、TT Probe ヒット時のカウント処理も行う。
- 反復深化の各深さごとの NodesSearched を構造化ログとして出力する。
- 既存の `FileHelper.Log()` ベースのログ機構を刷新し、構造化ログ（JSON Lines 形式）・ログレベル・日付ベースローテーション・自動クリーンアップを備えた `Logger` クラスを導入する。
- 本 RFC は後続の RFC 2〜4（Multi-ProbCut、Aspiration Window チューニング、時間制御）の前提となる計測基盤であり、可観測性の基盤でもある。

## 2. 背景・動機 (Motivation)

- 現在の Reluca 探索エンジンには、探索効率を定量的に評価する手段が存在しない。`SearchResult` は `BestMove` と `Value` のみを保持しており、探索にどれだけのノードを展開したかを知ることができない。
- 後続の RFC（Multi-ProbCut、Aspiration Window チューニング、時間制御）では、いずれも「探索効率の改善」を主指標として評価する必要がある。計測基盤がなければ、これらの改善効果を定量的に検証できない。
- WZebra のアルゴリズム組み込みロードマップにおいて、NodesSearched 計測は全 RFC の前提条件として位置付けられている。
- 放置した場合、TT の ON/OFF や Aspiration Window の効果を数値で示すことができず、パラメータ調整が経験則に頼ることになる。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- 探索完了時に NodesSearched が正の値として返されること
- TT ON/OFF で NodesSearched の差分が確認できること（TT ON 時に減少）
- 反復深化の各深さごとの NodesSearched を確認できること
- 既存の探索結果（BestMove、Value）に一切影響を与えないこと
- 構造化ログ（JSON Lines 形式）によるファイル出力で、探索統計を永続化すること
- ログの日付ベースローテーションと古いログの自動クリーンアップにより、運用負荷を低減すること

### やらないこと (Non-Goals)

- NPS（Nodes Per Second）の計測。時間計測は RFC 4（time-limit-search）のスコープとする
- LegacySearchEngine（CachedNegaMax）への NodesSearched 導入。PvsSearchEngine のみを対象とする
- UI への NodesSearched 表示
- 既存の `Console.WriteLine` 呼び出しの一括移行。既存コードへの Logger 導入は段階的に行う

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

ノード展開の定義は「局面に対して評価関数を呼び出すか、子ノードの探索を開始した時点」とする。具体的には以下のタイミングでインクリメントする。

| 箇所 | タイミング | 理由 |
|------|-----------|------|
| `Pvs()` メソッド先頭 | メソッド呼び出し時 | 再帰呼び出しされた全ノードをカウントする |
| `RootSearch()` の各子ノード展開 | ループ内で `Pvs()` を呼ぶ直前 | ルート局面の子ノードもカウントする |

**[仮定]** ルート局面自体は 1 ノードとしてカウントしない。ルート局面は合法手の列挙のみを行い、評価関数の呼び出しや探索の打ち切り判定が発生しないためである。ルートの子ノードは `Pvs()` メソッド呼び出し時にカウントされる。

#### TT Probe ヒット時の扱い

TT Probe ヒット時は `Pvs()` メソッドの先頭でカウント済みであるため、追加のカウント処理は不要である。TT Probe によるカットオフは「探索したが早期に打ち切れた」ノードとして 1 ノードにカウントされる。

これにより TT ON 時は「TT ヒットで打ち切られたノードは 1 としてカウントされるが、その先の子孫ノードは展開されないため NodesSearched が減少する」という挙動となり、TT の効果を定量的に評価できる。

#### 実装の詳細

**`Search()` メソッド（反復深化ループ）:**

```csharp
public SearchResult Search(GameContext context, SearchOptions options, IEvaluable evaluator)
{
    // ... 既存の初期化処理 ...

    long totalNodesSearched = 0;

    for (int depth = 1; depth <= options.MaxDepth; depth++)
    {
        _nodesSearched = 0;  // 深さごとにリセット

        // ... 既存の探索処理（AspirationRootSearch or RootSearch）...

        totalNodesSearched += _nodesSearched;

        // 深さごとのノード数を構造化ログとして出力
        Logger.Info("Iterative deepening completed", "PvsSearchEngine", new Dictionary<string, object>
        {
            { "depth", depth },
            { "nodes", _nodesSearched },
            { "total", totalNodesSearched },
            { "value", maxValue }
        });
    }

    return new SearchResult(bestMoveResult, maxValueResult, totalNodesSearched);
}
```

**[仮定]** Aspiration Window の再探索（retry）で展開されたノードも含めてカウントする。再探索は実際に探索処理を行っており、探索コストの一部であるためである。

**`Pvs()` メソッド先頭:**

```csharp
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)
{
    _nodesSearched++;  // ノードカウント

    // 終了条件チェック
    if (remainingDepth == 0 || IsGameEndTurnCount(context))
    {
        return Evaluate(context);
    }

    // TT Probe（ヒット時は早期リターン、カウント済み）
    // ... 既存処理 ...
}
```

### 5.3 Logger クラスの導入

既存の `FileHelper.Log()` はプレーンテキスト形式でログレベルの区分もなく、後続 RFC で求められる可観測性の要件を満たさない。abel プロジェクト（`~/projects/abel/src/helpers/logger.js`）のログ機構を参考に、C# 版の構造化ロガーを新規導入する。

#### 5.3.1 既存ログ機構の課題

| 課題 | 説明 |
|------|------|
| ログレベルなし | DEBUG / INFO / WARN / ERROR の区分がない |
| 構造化されていない | プレーンテキスト形式で自動解析が困難 |
| ローテートなし | ファイルが際限なく増加する |
| タイムスタンプなし | ログエントリにタイムスタンプが付与されない |

#### 5.3.2 Logger クラスの設計

`Reluca/Helpers/Logger.cs` に新規クラスを追加する。

```csharp
/// <summary>
/// 構造化ログを提供するロガー
/// </summary>
public static class Logger
{
    /// <summary>
    /// ログレベル
    /// </summary>
    public enum Level
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// ログ出力ディレクトリ
    /// </summary>
    private static string _logDir = "./logs";

    /// <summary>
    /// 現在のログレベル
    /// </summary>
    private static Level _level = Level.Info;

    /// <summary>
    /// ログ保持日数
    /// </summary>
    private static int _maxDays = 30;

    /// <summary>
    /// 標準出力への出力有無
    /// </summary>
    private static bool _stdout = true;

    /// <summary>
    /// 前回クリーンアップを実行した日付
    /// </summary>
    private static string _lastCleanupDate = "";

    /// <summary>
    /// ロガーを初期化する
    /// </summary>
    /// <param name="logDir">ログ出力ディレクトリ</param>
    /// <param name="level">ログレベル</param>
    /// <param name="maxDays">ログ保持日数</param>
    /// <param name="stdout">標準出力への出力有無</param>
    public static void Init(string logDir = "./logs", Level level = Level.Info, int maxDays = 30, bool stdout = true)
    {
        _logDir = logDir;
        _level = level;
        _maxDays = maxDays;
        _stdout = stdout;
    }

    /// <summary>
    /// DEBUG レベルのログを出力する
    /// </summary>
    public static void Debug(string message, string component = "", Dictionary<string, object>? fields = null) => Log(Level.Debug, message, component, fields);

    /// <summary>
    /// INFO レベルのログを出力する
    /// </summary>
    public static void Info(string message, string component = "", Dictionary<string, object>? fields = null) => Log(Level.Info, message, component, fields);

    /// <summary>
    /// WARN レベルのログを出力する
    /// </summary>
    public static void Warn(string message, string component = "", Dictionary<string, object>? fields = null) => Log(Level.Warn, message, component, fields);

    /// <summary>
    /// ERROR レベルのログを出力する
    /// </summary>
    public static void Error(string message, string component = "", Dictionary<string, object>? fields = null) => Log(Level.Error, message, component, fields);
}
```

#### 5.3.3 ログ出力形式

abel プロジェクトと同様に、出力先に応じて形式を分ける。

**ファイル出力（JSON Lines 形式）:**

```json
{"ts":"2026-02-05T12:00:00.000Z","level":"INFO","component":"PvsSearchEngine","msg":"Iterative deepening completed","depth":6,"nodes":12345,"total":54321,"value":150}
```

**標準出力（テキスト形式）:**

```
[2026-02-05T12:00:00.000Z] [INFO] [PvsSearchEngine] Iterative deepening completed depth=6 nodes=12345 total=54321 value=150
```

**[仮定]** JSON シリアライズには既に依存関係にある `Newtonsoft.Json` を使用する。追加の NuGet パッケージは導入しない。

#### 5.3.4 日付ベースローテーション

ログファイル名に日付を含め、日ごとに自動的に新ファイルを生成する。

- **ファイル名パターン**: `reluca-YYYY-MM-DD.log`
- **出力先**: `./logs/` ディレクトリ
- **日付切り替え検知**: ログ書き込み時に現在日付とファイル名の日付を比較する

#### 5.3.5 古いログの自動クリーンアップ

`_maxDays`（デフォルト 30 日）以上前のログファイルを自動削除する。

- **実行タイミング**: 日付が変わった最初のログ書き込み時
- **削除対象**: `reluca-YYYY-MM-DD.log` パターンに一致するファイルのうち、`_maxDays` 日以上前のもの
- **エラー処理**: 削除失敗時はコンソールに警告を出力し、アプリケーションの動作は継続する

#### 5.3.6 FileHelper.Log() との関係

既存の `FileHelper.Log()` は残置する。新規コードでは `Logger` クラスを使用し、既存コードの `FileHelper.Log()` は段階的に移行する方針とする。

### 5.4 探索ログの出力

反復深化の各深さ完了時に、以下の情報を構造化ログとして出力する。

| フィールド | 説明 |
|-----------|------|
| `depth` | 探索深さ |
| `nodes` | 当該深さでの探索ノード数 |
| `total` | 累計探索ノード数 |
| `value` | 当該深さでの評価値 |

```csharp
Logger.Info("Iterative deepening completed", "PvsSearchEngine", new Dictionary<string, object>
{
    { "depth", depth },
    { "nodes", _nodesSearched },
    { "total", totalNodesSearched },
    { "value", maxValue }
});
```

**[仮定]** 探索ログは `Info` レベルとする。後続 RFC でのパラメータ調整時に常に確認したい情報であり、DEBUG レベルでは運用時に見落とすリスクがあるためである。

### 5.5 ISearchEngine インターフェースへの影響

`ISearchEngine` インターフェースの `Search()` メソッドシグネチャは変更しない。戻り値の `SearchResult` に `NodesSearched` が追加されるのみであり、`SearchResult` のコンストラクタにデフォルト値を設定することで後方互換性を維持する。

### 5.6 変更対象ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Reluca/Helpers/Logger.cs` | 新規作成。構造化ログ、ログレベル、日付ベースローテーション、自動クリーンアップを提供する Logger クラス |
| `Reluca/Search/SearchResult.cs` | `NodesSearched` プロパティ追加、コンストラクタ拡張 |
| `Reluca/Search/PvsSearchEngine.cs` | `_nodesSearched` フィールド追加、カウントロジック、Logger による構造化ログ出力 |

## 6. 代替案の検討 (Alternatives Considered)

### 案A: インスタンスフィールドによるカウント（採用案）

- **概要**: `PvsSearchEngine` にインスタンスフィールド `_nodesSearched` を持ち、探索メソッド内で直接インクリメントする。
- **長所**: 実装がシンプル。追加のオブジェクト生成や間接参照がなく、パフォーマンスへの影響が最小。既存の `_bestMove` や `_currentDepth` と同じパターンで一貫性がある。
- **短所**: `PvsSearchEngine` の責務が若干増える。スレッドセーフではないが、現状シングルスレッド探索のため問題にならない。

### 案B: SearchStatistics クラスの導入

- **概要**: `SearchStatistics` クラスを新設し、NodesSearched をはじめとする各種統計情報（TT ヒット数、ベータカット数等）をまとめて管理する。
- **長所**: 将来的に統計情報を拡張しやすい。関心の分離が明確。
- **短所**: 現時点では NodesSearched のみが必要であり、クラス新設はオーバーエンジニアリングである。`PvsSearchEngine` から `SearchStatistics` への参照渡しが必要になり、再帰メソッドのシグネチャが変わるか、フィールドとして保持する場合は案 A と本質的に同じになる。

### 選定理由

現時点で必要な計測指標は NodesSearched のみである。案 B は将来の拡張性で優れるが、YAGNI の原則に基づき、必要最小限の変更で目的を達成できる案 A を採用する。将来 TT ヒット率やベータカット率の計測が必要になった時点で、案 B へのリファクタリングを検討すればよい。

### ログ機構の代替案

#### 案C: Microsoft.Extensions.Logging の採用

- **概要**: .NET 標準のログ抽象化レイヤーである `Microsoft.Extensions.Logging` を導入し、ファイル出力には `Serilog` 等のシンクを組み合わせる。
- **長所**: .NET エコシステムの標準パターン。DI との統合が容易。将来的にログプロバイダの差し替えが可能。
- **短所**: NuGet パッケージの追加依存が発生する。Reluca はライブラリであり、ホストアプリケーションのログ設定に依存する形になる。現在の DI 構成（`DiProvider` による静的サービスプロバイダ）との統合に追加作業が必要。

#### 案D: 独自 Logger クラスの導入（採用案）

- **概要**: abel プロジェクトのログ機構を参考に、C# で独自の静的 Logger クラスを実装する。JSON Lines 形式の構造化ログ、日付ベースローテーション、自動クリーンアップを自前で実装する。
- **長所**: 外部依存なし。abel で実績のある設計パターンを流用できる。既存の `FileHelper` と同じく静的クラスとして実装するため、既存コードとの一貫性が高い。
- **短所**: ローテーションやクリーンアップのロジックを自前で実装・保守する必要がある。

#### 選定理由（ログ機構）

Reluca はオセロ思考エンジンのライブラリであり、ログ出力の要件は限定的である。Microsoft.Extensions.Logging の導入は依存関係の増加に見合うメリットが薄い。abel プロジェクトで実績のある独自実装パターンが Reluca の規模に適しており、外部依存を増やさずに構造化ログ・ローテーション・クリーンアップを実現できる案 D を採用する。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

- `_nodesSearched++` は `long` 型の単純なインクリメントであり、探索ノードあたりのオーバーヘッドは無視できる水準である。
- ログ出力は反復深化の各深さ完了時（最大 MaxDepth 回）のみであり、ノード展開ごとではないためパフォーマンスに影響しない。
- `SearchResult` への `NodesSearched` 追加は `long` 型フィールド 1 つであり、メモリへの影響は無視できる。

### 7.2 ログ機構の運用

- ログファイルは `./logs/reluca-YYYY-MM-DD.log` に日付単位で出力される。
- 30 日以上前のログファイルは自動削除される。デフォルト設定では手動でのログ管理は不要である。
- `Logger.Init()` により、ログディレクトリ・ログレベル・保持日数・標準出力の有無を設定可能とする。
- ファイル書き込みに失敗した場合は標準出力にフォールバックし、アプリケーションの動作は継続する。

### 7.3 マイグレーションと後方互換性

- `SearchResult` のコンストラクタにデフォルト値（`nodesSearched = 0`）を設定するため、既存の呼び出し元（`LegacySearchEngine` 等）は変更不要である。
- `ISearchEngine` インターフェースの変更はない。
- 既存の `FileHelper.Log()` は残置するため、既存のログ呼び出しに影響はない。
- 破壊的変更はない。

## 8. テスト戦略 (Test Strategy)

### ユニットテスト

#### NodesSearched

| テスト観点 | 内容 |
|-----------|------|
| 正の値の検証 | 探索完了時に `NodesSearched > 0` であること |
| TT 効果の検証 | 同一局面で TT ON 時の NodesSearched が TT OFF 時より少ないこと |
| 探索結果の非干渉 | NodesSearched 導入前後で `BestMove` と `Value` が一致すること |
| 深さごとの累積 | 反復深化の全深さの NodesSearched 合計が `SearchResult.NodesSearched` と一致すること |

#### Logger

| テスト観点 | 内容 |
|-----------|------|
| ログファイル生成 | `Logger.Info()` 呼び出し後にログファイルが生成されること |
| JSON Lines 形式 | ログファイルの各行が有効な JSON であること |
| 必須フィールド | `ts`, `level`, `msg` フィールドが含まれること |
| ログレベルフィルタ | 設定レベル未満のログが出力されないこと |
| 日付ローテーション | 日付の異なるログが別ファイルに出力されること |
| 自動クリーンアップ | 保持日数を超えたログファイルが削除されること |

### 検証方法

- 既存テストを全て実行し、探索結果に変更がないことを確認する。
- テスト用の固定局面で TT ON/OFF それぞれの NodesSearched を取得し、TT ON 時に減少していることを検証する。
- Logger のテストでは一時ディレクトリを使用し、テスト後にクリーンアップする。

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: Logger クラスの導入

- `Reluca/Helpers/Logger.cs` を新規作成する
- 構造化ログ（JSON Lines 形式）の出力を実装する
- ログレベル（DEBUG / INFO / WARN / ERROR）のフィルタリングを実装する
- 日付ベースローテーション（`reluca-YYYY-MM-DD.log`）を実装する
- 古いログの自動クリーンアップ（デフォルト 30 日）を実装する
- Logger のユニットテストを作成し通過することを確認する

### フェーズ 2: SearchResult 拡張

- `SearchResult` に `NodesSearched` プロパティを追加する
- コンストラクタにデフォルト値付きパラメータを追加する
- 既存テストが通過することを確認する

### フェーズ 3: カウンタ実装とログ出力

- `PvsSearchEngine` に `_nodesSearched` フィールドを追加する
- `Pvs()` メソッド先頭でのカウント処理を追加する
- `Search()` メソッドで深さごとのリセットと累計を実装する
- `SearchResult` 生成時に `totalNodesSearched` を渡す
- 反復深化の各深さ完了時に Logger を使用して構造化ログを出力する

### フェーズ 4: テストと検証

- NodesSearched が正の値であることを検証するテストを追加する
- TT ON/OFF での NodesSearched 差分を検証するテストを追加する
- 既存テストが全て通過することを確認する

### システム概要ドキュメントへの影響

- `docs/architecture.md`: `Logger` クラスの追加を Helpers セクションに反映する。
- `docs/domain-model.md`: 影響なし。ドメイン概念やデータモデルに変更はない。
- `docs/api-overview.md`: 存在しない（対象外）。
