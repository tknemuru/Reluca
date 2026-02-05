# [RFC] 構造化ログ基盤の構築

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | Claude Code |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-05 |
| **タグ** | Helpers, Logging, 中優先度 |
| **関連リンク** | [WZebra RFC ロードマップ](../../reports/wzebra-rfc-roadmap.md), [RFC: nodes-searched-instrumentation](../20260205-nodes-searched-instrumentation/rfc.md) |

## 1. 要約 (Summary)

- Reluca に構造化ログ基盤を導入する。`Microsoft.Extensions.Logging`（抽象層）+ `Serilog`（実装層）を採用し、JSON 形式の構造化ログ出力とファイルローテーションを実現する。
- 既存の `Console.WriteLine` によるログ出力を `ILogger<T>` に置き換え、テスト時のモック差し替えを可能にする。
- 本 RFC は WZebra RFC ロードマップの前提基盤として位置づけ、RFC 2（multi-probcut）以降の性能分析・デバッグを支援する。

## 2. 背景・動機 (Motivation)

### 2.1 現状の課題

RFC 1（nodes-searched-instrumentation）で `PvsSearchEngine` に探索統計の出力を追加した。現在は `Console.WriteLine` で `depth=8 nodes=154320 total=523100 value=12` のようなプレーンテキストを出力している（`PvsSearchEngine.cs:175`）。

この方式には以下の課題がある。

- **構造化されていない**: プレーンテキストのため、Claude Code や外部ツールによるプログラム的な分析・集計が困難である。例えば「depth=10 の探索で nodes が最も多かった対局」を抽出するには、テキストパースが必要になる。
- **テスト非親和**: `Console.WriteLine` は差し替えが困難であり、テスト時にログ出力を制御できない。
- **ログレベルなし**: Debug/Info/Warn/Error の区別がなく、本番実行時に冗長な出力を抑制する手段がない。
- **ファイル出力・ローテーション機構なし**: 現在の `Console.WriteLine` 出力は標準出力のみであり、永続化されない。ファイルに保存して事後分析する仕組みがない。

### 2.2 この段階で構造化ログ基盤を導入する理由

現時点でのログ出力箇所は `PvsSearchEngine` の 1 箇所のみであり、「出力フォーマットを JSON 文字列に変えるだけ」で最低限の構造化は実現できる。しかし、以下の理由から DI ベースのログ基盤を今の段階で導入する。

1. **後続 RFC でのログ出力箇所の増加が確実である**: RFC ロードマップには multi-probcut（RFC 2）、move-ordering 改善（RFC 5）等の探索アルゴリズム改善が予定されている。これらの RFC では枝刈り率、手順の優先度スコア、キャッシュヒット率などの計測ログが追加される。ログ出力箇所が増えてから基盤を導入すると、移行コストが膨らむ。
2. **DI 注入による段階的導入**: `ILogger<T>` を DI で注入する方式であれば、後続 RFC で新しいクラスにログを追加する際にコンストラクタに `ILogger<T>` を追加するだけでよく、追加コストが最小化される。基盤が存在しない状態でクラスごとに `Console.WriteLine` を使い続けると、後からの一括移行が困難になる。
3. **費用対効果**: 外部パッケージ 5 つの追加と `DiProvider` への数行の構成追加で、JSON 構造化出力・ファイルローテーション・ログレベル制御・テスト容易性が全て実現する。自前実装と比較して初期コスト・保守コストともに低い。

### 2.3 FileHelper.Log() との関係

`FileHelper.Log()` はタイムスタンプ付きファイルへの書き込みメソッドであり、`Reluca.Tools/ValidStateExtractor.cs` で使用されている。ただし、`FileHelper` は static クラスであるため、DI によるモック差し替えが不可能であり、ファイルローテーション機構も備えていない。本 RFC のスコープである `Reluca` 本体コードのログ出力には適さない。`Reluca.Tools` での `FileHelper.Log()` の利用は本 RFC のスコープ外とし、将来の対応に委ねる。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- JSON 形式の構造化ログを出力し、プログラム的な分析が容易な状態にする。
- ファイルローテーション（日次 + サイズ上限）を導入し、ディスク容量を管理する。
- `ILogger<T>` インターフェースによるコンストラクタ DI を導入し、テスト時にモック/NullLogger への差し替えを可能にする。
- 既存の `Console.WriteLine` によるログ出力箇所（本体コード）を新機構に書き換える。

### やらないこと (Non-Goals)

- テストコード内の `Console.WriteLine`（テスト診断出力）の書き換えは行わない。
- `Reluca.Tools` / `Reluca.Ui.WinForms` のログ出力書き換えは行わない（将来対応）。
- `FileHelper` クラスの削除・大規模リファクタリングは行わない。ファイル I/O ユーティリティとしての機能は維持する。
- 分散トレーシングやメトリクス収集基盤の導入は行わない。
- ログの可視化ダッシュボードの構築は行わない。
- ログ出力先パスの外部設定ファイル化（`appsettings.json` 等）は行わない。ログ出力箇所が増えた段階で必要に応じて検討する。

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- .NET 8.0 ランタイム（現行環境で充足済み）。
- `Microsoft.Extensions.DependencyInjection` v8.0.0（導入済み）。
- 新規 NuGet パッケージの追加:
  - `Serilog` v4.x
  - `Serilog.Extensions.Hosting` v8.x（`Microsoft.Extensions.Logging` 統合）
  - `Serilog.Sinks.Console`（コンソール出力）
  - `Serilog.Sinks.File`（ファイル出力 + ローテーション）
- RFC 1（nodes-searched-instrumentation）が完了していること（完了済み）。

## 5. 詳細設計 (Detailed Design)

### 5.1 パッケージ追加

`Reluca/Reluca.csproj` に以下のパッケージを追加する。

```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.1" />
<PackageReference Include="Serilog" Version="4.2.0" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
```

`Microsoft.Extensions.Logging` は `Serilog.Extensions.Hosting` の推移的依存に含まれるが、`ILogger<T>` を直接利用するパッケージとして明示的に追加する。推移的依存に頼ると、Serilog パッケージの更新時に暗黙的にバージョンが変わるリスクがあるためである。

**[仮定]** バージョンは .NET 8.0 互換の最新安定版を想定している。実装時に NuGet で最新安定版を確認し、適宜調整する。

### 5.2 Serilog 設定

`DiProvider.BuildDefaultProvider()` にて Serilog の構成と `ILogger<T>` の DI 登録を行う。

```csharp
using Microsoft.Extensions.Logging;
using Serilog;

private static ServiceProvider BuildDefaultProvider()
{
    // Serilog 構成（DI コンテナ内で完結させ、グローバル静的ロガーへの代入は行わない）
    var serilogLogger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
        .WriteTo.File(
            new Serilog.Formatting.Json.JsonFormatter(),
            "./log/structured/reluca-.json",
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10_000_000,   // 10MB
            retainedFileCountLimit: 30,        // 30世代保持
            rollOnFileSizeLimit: true)
        .CreateLogger();

    var services = new ServiceCollection();

    // MS Logging 統合
    services.AddLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddSerilog(serilogLogger, dispose: true);
    });

    // 既存の DI 登録...
    services.AddSingleton<StringToBoardContextConverter, ...>();
    // ...
}
```

#### 設計判断

- **グローバル静的ロガー `Log.Logger` を使用しない**: Serilog の `Log.Logger` はプロセス全体で共有されるグローバル静的インスタンスである。これを設定すると、DI コンテナ経由の `ILogger<T>` との二重管理になり、テストの並列実行時にグローバル状態が干渉するリスクがある。代わりに `AddSerilog(serilogLogger, dispose: true)` でローカル変数のロガーインスタンスを DI コンテナに閉じ込める。
- **アプリケーション終了時のフラッシュ**: `AddSerilog(serilogLogger, dispose: true)` を指定しているため、`ServiceProvider.Dispose()` 時に Serilog のロガーが自動的に `CloseAndFlush` される。`DiProvider` は静的クラスであり `ServiceProvider` の `Dispose` はプロセス終了時に呼ばれるが、ファイル Sink のデフォルト `flushToDiskInterval` は 1 秒であるため、通常の使用でログ欠損のリスクは低い。
- **ログファイル出力先**: `./log/structured/reluca-.json` とし、既存の `FileHelper` が出力する `./log/` 直下のファイルとディレクトリを分離する。これにより、Serilog のローテーションによる自動削除が `FileHelper` の出力ファイルに影響しない。
- **相対パスの採用**: ログファイルのパスは相対パス（カレントディレクトリ基準）を採用する。Reluca はローカル環境でのみ実行されるデスクトップアプリケーション / CLI ツールであり、実行ディレクトリはプロジェクトルートに固定されている。サーバーアプリケーションのように実行ディレクトリが不定になるケースは想定されないため、`AppContext.BaseDirectory` による絶対パス組み立ては不要と判断する。

#### ローテーション仕様

| パラメータ | 値 | 説明 |
|---|---|---|
| `rollingInterval` | `RollingInterval.Day` | 日次でファイルをローテーション |
| `fileSizeLimitBytes` | 10,000,000（10MB） | 1ファイルあたりのサイズ上限 |
| `rollOnFileSizeLimit` | `true` | サイズ超過時に新ファイルを作成 |
| `retainedFileCountLimit` | 30 | 最大30世代のファイルを保持（超過分は自動削除） |

**最大ディスク使用量**: 10MB × 30世代 = 最大約 300MB である。現在のログ出力は反復深化の各深さ完了時（最大 16 回/探索）に限定されるため、1 対局あたりのログ量は数 KB 程度であり、日常の開発・実験で 10MB/日に達することはほぼない。300MB はローカル開発環境で十分に許容範囲である。

#### JSON 出力フォーマット例

```json
{
  "Timestamp": "2026-02-05T14:30:00.123+09:00",
  "Level": "Information",
  "MessageTemplate": "探索進捗 {@SearchProgress}",
  "Properties": {
    "SearchProgress": {
      "Depth": 8,
      "Nodes": 154320,
      "TotalNodes": 523100,
      "Value": 12
    },
    "SourceContext": "Reluca.Search.PvsSearchEngine"
  }
}
```

`SourceContext` は `ILogger<T>` の `T` から自動付与されるため、出力元クラスの追跡が容易である。

### 5.3 利用側の変更: PvsSearchEngine

`PvsSearchEngine` のコンストラクタに `ILogger<PvsSearchEngine>` を追加し、`Console.WriteLine` を `ILogger` 呼び出しに置き換える。

**変更前**（`PvsSearchEngine.cs:175`）:

```csharp
Console.WriteLine($"depth={depth} nodes={_nodesSearched} total={totalNodesSearched} value={result.Value}");
```

**変更後**:

```csharp
private readonly ILogger<PvsSearchEngine> _logger;

public PvsSearchEngine(
    ILogger<PvsSearchEngine> logger,
    MobilityAnalyzer mobilityAnalyzer,
    MoveAndReverseUpdater reverseUpdater,
    ITranspositionTable transpositionTable,
    IZobristHash zobristHash)
{
    _logger = logger;
    _mobilityAnalyzer = mobilityAnalyzer;
    _reverseUpdater = reverseUpdater;
    _transpositionTable = transpositionTable;
    _zobristHash = zobristHash;
    _bestMove = -1;
}

// 探索進捗出力
_logger.LogInformation("探索進捗 {@SearchProgress}", new
{
    Depth = depth,
    Nodes = _nodesSearched,
    TotalNodes = totalNodesSearched,
    Value = result.Value
});
```

構造化パラメータ `@SearchProgress` により、JSON 出力時にオブジェクトが展開される。コンソール出力時は `lj`（リテラル JSON）フォーマットで可読性を維持する。

### 5.4 DI 登録への反映

`DiProvider.BuildDefaultProvider()` での `PvsSearchEngine` の登録は変更不要である。`Microsoft.Extensions.DependencyInjection` が `ILogger<PvsSearchEngine>` を自動解決する。

### 5.5 テスト時の扱い

既存のテストはすべて `DiProvider.Get().GetService<PvsSearchEngine>()` により DI 経由でインスタンスを取得している。`DiProvider` に `AddLogging` を追加することで、DI コンテナが `ILogger<PvsSearchEngine>` を自動解決するため、**既存テストのコード変更は不要**である。

`DiProvider` は静的コンストラクタで初期化されるため、テスト実行時にも `BuildDefaultProvider()` 内の Serilog 構成が適用される。テストではログ出力が `./log/structured/` に書き込まれるが、テストの動作に影響はない。テスト実行時にログ出力を完全に抑制したい場合は、テスト用の `ServiceProvider` を別途構築し `NullLoggerProvider` を使用する方式が考えられるが、現時点では DI 経由の標準構成で十分である。

`PvsSearchEngine` を手動生成する場合（将来的にテストで直接インスタンスを構築する場合）は、`NullLoggerFactory` を使用する。

```csharp
using Microsoft.Extensions.Logging.Abstractions;

var logger = NullLoggerFactory.Instance.CreateLogger<PvsSearchEngine>();
var engine = new PvsSearchEngine(logger, mobilityAnalyzer, reverseUpdater, tt, zobristHash);
```

### 5.6 FileHelper.Log() の扱い

`FileHelper.Log()` メソッドは `Reluca.Tools/ValidStateExtractor.cs` で使用されている。`Reluca.Tools` のログ出力書き換えは本 RFC のスコープ外（Non-Goals）であるため、`FileHelper.Log()` は現状のまま維持する。`FileHelper` クラスのその他の I/O ユーティリティメソッド（`ReadJson`, `WriteJson`, `ReadTextLines` 等）は引き続き使用されるため、クラス自体の削除は行わない。

### 5.7 変更対象ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `Reluca/Reluca.csproj` | NuGet パッケージ追加（5件） |
| `Reluca/Di/DiProvider.cs` | Serilog 構成 + `AddLogging` 追加 |
| `Reluca/Search/PvsSearchEngine.cs` | `ILogger<T>` の DI 注入 + `Console.WriteLine` 置き換え |

既存テストは DI 経由でインスタンスを取得しているため、コード変更は不要である。

## 6. 代替案の検討 (Alternatives Considered)

### 案A: 自作 Logger クラス（static クラス方式）

- **概要**: RFC 1 で当初検討されていた方式。`static class Logger` を定義し、`Logger.Info(...)` のように呼び出す。
- **長所**: 外部依存なし。実装がシンプル。
- **短所**:
  - static メソッドはモック不可。テスト時にログ出力の制御・検証ができない。
  - 構造化ログ、ファイルローテーション、ログレベル制御を全て自前実装する必要がある。
  - 車輪の再発明であり、保守コストが高い。

### 案B: 自作 Logger クラス（インターフェース + DI 方式）

- **概要**: `ILogger` インターフェースを自作し、DI で注入する。テスト時はモック実装に差し替え可能。
- **長所**: 外部依存なし。テスト容易性が確保される。
- **短所**:
  - 構造化ログ出力（JSON フォーマッタ）を自前実装する必要がある。
  - ファイルローテーションの実装が必要（信頼性の担保が課題）。
  - `Microsoft.Extensions.Logging` と名前が衝突するため、名前空間の管理が煩雑になる。

### 案C: Microsoft.Extensions.Logging + Serilog（採用案）

- **概要**: .NET 標準のロギング抽象層と、構造化ログのデファクトスタンダードを組み合わせる。
- **長所**:
  - `ILogger<T>` による DI 注入でテスト容易性が高い。
  - JSON 構造化ログ、ファイルローテーションがライブラリの設定のみで実現する。
  - 既存の `Microsoft.Extensions.DependencyInjection` と同じエコシステムで統一される。
  - .NET エコシステムで広く採用されており、ドキュメント・サポートが充実している。
- **短所**:
  - 外部パッケージ 5 つの追加が必要。
  - Serilog のバージョンアップへの追従が必要。

### 案D: NLog

- **概要**: .NET の老舗ロギングフレームワーク。`Microsoft.Extensions.Logging` との統合も可能。
- **長所**: 枯れた実装で安定性が高い。XML 設定による柔軟な構成。
- **短所**:
  - 構造化ログは後付け対応であり、Serilog ほどネイティブではない。
  - XML 設定が主流で、コードベースの Fluent 設定は Serilog の方が簡潔。
  - Reluca の要件（JSON 構造化 + プログラム分析）には Serilog のネイティブ構造化ログが最適。

### 案E: Console.WriteLine の出力フォーマットを JSON 化するのみ（段階的アプローチ）

- **概要**: 外部パッケージを追加せず、`Console.WriteLine` の出力を `System.Text.Json` で JSON 文字列に変えるだけの最小変更。
- **長所**: 外部依存なし。変更範囲が最小。構造化された出力は実現する。
- **短所**:
  - `Console.WriteLine` のままであるため、テスト時のモック差し替えが不可能。
  - ファイルへの永続化・ローテーションは別途実装が必要。
  - ログレベル制御が不可能。後続 RFC でログ出力箇所が増えた際に、再び基盤導入の RFC が必要になる。
  - DI 注入への移行時に、全てのクラスのコンストラクタ変更が一度に発生する。

### 選定理由

案C を採用する。決定的な差異は以下の通り。

1. **テスト容易性**: 案A/E（static / `Console.WriteLine`）はモック不可で却下。案B/C/D はいずれも DI 対応だが、案B は自前実装の保守コストが高い。
2. **構造化ログのネイティブ対応**: Serilog は設計思想の中核に構造化ログを据えており、`@` によるオブジェクト展開が自然に機能する。NLog の構造化ログは後付けで記述が冗長になる。
3. **既存エコシステムとの統合**: `Microsoft.Extensions.DependencyInjection` を既に使用しており、同ファミリーの `Microsoft.Extensions.Logging` + Serilog の組み合わせが最も自然である。
4. **段階的導入の初期コスト**: 案E は最小変更だが、後続 RFC でのログ出力追加のたびに基盤問題が再浮上する。案C は初期コストがわずかに高いが、後続の追加コストが最小化される。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

- ログに個人情報や機密情報は含まれない。ゲーム探索の統計データ（深さ、ノード数、評価値）のみが出力対象である。
- ログファイルの出力先は `./log/structured/` ディレクトリであり、アプリケーションのローカル環境にのみ保存される。

### 7.2 スケーラビリティとパフォーマンス

- 探索ループ内でのロギング呼び出しがパフォーマンスに影響する可能性がある。ただし、現在のログ出力箇所は反復深化の各深さ完了時（1回/depth）のみであり、探索ノードごとではない。深さ 1〜16 で最大 16 回/探索であるため、パフォーマンスへの影響は無視できる。
- 将来、探索ノードごとの Debug ログを追加する場合は、`ILogger.IsEnabled(LogLevel.Debug)` によるガード条件を設けることで、ログレベルが Information 以上の場合にオーバーヘッドを回避する。

### 7.3 可観測性 (Observability)

- 本 RFC 自体がログ基盤の導入であり、可観測性の第一歩に位置づけられる。
- 構造化ログにより、`jq` やスクリプトによるフィルタリング・集計が可能になる。例: `jq 'select(.Properties.SearchProgress.Depth == 10)' reluca-20260205.json`

### 7.4 マイグレーションと後方互換性

- `PvsSearchEngine` のコンストラクタに `ILogger<PvsSearchEngine>` パラメータが追加される。ただし、既存テストは全て DI 経由（`DiProvider.Get().GetService<PvsSearchEngine>()`）でインスタンスを取得しているため、テストコードの変更は不要である。
- コンソール出力のフォーマットが変更される。従来の `depth=8 nodes=154320 total=523100 value=12` 形式から Serilog のテンプレート形式に変わる。コンソール出力をパースしている外部ツール・スクリプトがある場合は対応が必要だが、現時点では該当なし。
- ロールバック手順: NuGet パッケージの削除と `Console.WriteLine` への復元で元に戻せる。破壊的変更はコンストラクタシグネチャのみ。

## 8. テスト戦略 (Test Strategy)

- **ユニットテスト**:
  - `PvsSearchEngine` の既存テスト全件が変更なしで通過すること（DI 経由のため `ILogger` は自動解決される）。
  - 探索結果（BestMove, Value, NodesSearched）が変更前と一致すること。
- **統合テスト**:
  - `DiProvider.Get()` から `PvsSearchEngine` を解決し、`ILogger<PvsSearchEngine>` が正しく注入されていることを確認する。
  - 実際の探索実行後に `./log/structured/` 配下に JSON ログファイルが生成されることを確認する。
- **手動検証**:
  - 生成された JSON ログファイルが `jq` でパース可能であることを確認する。
  - ローテーションの動作確認（日付境界またはサイズ上限でファイルが分割されること）。

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: 基盤導入

1. NuGet パッケージの追加（`Reluca.csproj`）
2. `DiProvider` に Serilog 構成と `AddLogging` を追加
3. ビルド確認

### フェーズ 2: PvsSearchEngine のログ置き換え

1. `PvsSearchEngine` コンストラクタに `ILogger<PvsSearchEngine>` を追加
2. `Console.WriteLine`（175行目）を `_logger.LogInformation` に置き換え
3. 全テスト通過を確認（既存テストはコード変更不要）

### フェーズ 3: 検証

1. JSON ログファイルの出力内容確認
2. `jq` によるパース・集計の動作確認
3. ローテーション動作の確認

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: 技術スタック表に `Serilog`（ログ基盤）を追加する。
- **`docs/domain-model.md`**: 影響なし（ドメインモデルの変更はない）。
