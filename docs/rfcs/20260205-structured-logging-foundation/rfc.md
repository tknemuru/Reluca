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

- RFC 1（nodes-searched-instrumentation）で `PvsSearchEngine` に探索統計の出力を追加した。現在は `Console.WriteLine` で `depth=8 nodes=154320 total=523100 value=12` のようなプレーンテキストを出力している（`PvsSearchEngine.cs:175`）。
- この方式には以下の課題がある。
  - **構造化されていない**: プレーンテキストのため、Claude Code や外部ツールによるプログラム的な分析・集計が困難である。
  - **ローテーションなし**: 既存の `FileHelper.Log()` はタイムスタンプ付きファイルに書き込むが、ファイルサイズ制限やローテーション機構がなく、長時間の探索実験でディスクを圧迫するリスクがある。
  - **テスト非親和**: `Console.WriteLine` や `FileHelper`（static クラス）は差し替えが困難であり、テスト時にログ出力を制御できない。
  - **ログレベルなし**: Debug/Info/Warn/Error の区別がなく、本番実行時に冗長な出力を抑制する手段がない。
- RFC 1 策定時に「構造化ログ基盤の構築」はスコープ外として先送りされた。次の RFC（multi-probcut 等）では探索統計の定量分析がさらに重要になるため、この段階で基盤を整備する。

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

**[仮定]** バージョンは .NET 8.0 互換の最新安定版を想定している。実装時に NuGet で最新安定版を確認し、適宜調整する。

### 5.2 Serilog 設定

`DiProvider.BuildDefaultProvider()` にて Serilog の構成と `ILogger<T>` の DI 登録を行う。

```csharp
using Microsoft.Extensions.Logging;
using Serilog;

private static ServiceProvider BuildDefaultProvider()
{
    // Serilog 構成
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
        .WriteTo.File(
            new Serilog.Formatting.Json.JsonFormatter(),
            "./log/reluca-.json",
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
        builder.AddSerilog(dispose: true);
    });

    // 既存の DI 登録...
    services.AddSingleton<StringToBoardContextConverter, ...>();
    // ...
}
```

#### ローテーション仕様

| パラメータ | 値 | 説明 |
|---|---|---|
| `rollingInterval` | `RollingInterval.Day` | 日次でファイルをローテーション |
| `fileSizeLimitBytes` | 10,000,000（10MB） | 1ファイルあたりのサイズ上限 |
| `rollOnFileSizeLimit` | `true` | サイズ超過時に新ファイルを作成 |
| `retainedFileCountLimit` | 30 | 最大30世代のファイルを保持（超過分は自動削除） |

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

テストコードでは `NullLoggerFactory` を使用してログ出力を無効化する。

```csharp
using Microsoft.Extensions.Logging.Abstractions;

var logger = NullLoggerFactory.Instance.CreateLogger<PvsSearchEngine>();
var engine = new PvsSearchEngine(logger, mobilityAnalyzer, reverseUpdater, tt, zobristHash);
```

テストの DI コンテナに `AddLogging` を追加する場合は `NullLoggerProvider` を登録する。

### 5.6 FileHelper.Log() の扱い

`FileHelper.Log()` メソッドは現在使用箇所がない。本 RFC では削除せず、既存のファイル I/O ユーティリティとして `FileHelper` を維持する。将来的に使用箇所がないことが確認された場合に削除を検討する。

### 5.7 変更対象ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `Reluca/Reluca.csproj` | NuGet パッケージ追加（5件） |
| `Reluca/Di/DiProvider.cs` | Serilog 構成 + `AddLogging` 追加 |
| `Reluca/Search/PvsSearchEngine.cs` | `ILogger<T>` の DI 注入 + `Console.WriteLine` 置き換え |
| `Reluca.Tests/Search/PvsSearchEngineNodesSearchedUnitTest.cs` | `NullLogger` の注入に対応 |
| `Reluca.Tests/` 配下（PvsSearchEngine を直接生成しているテスト） | コンストラクタ引数の追加に対応 |

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

### 選定理由

案C を採用する。決定的な差異は以下の通り。

1. **テスト容易性**: 案A（static）はモック不可で却下。案B/C/D はいずれも DI 対応だが、案B は自前実装の保守コストが高い。
2. **構造化ログのネイティブ対応**: Serilog は設計思想の中核に構造化ログを据えており、`@` によるオブジェクト展開が自然に機能する。NLog の構造化ログは後付けで記述が冗長になる。
3. **既存エコシステムとの統合**: `Microsoft.Extensions.DependencyInjection` を既に使用しており、同ファミリーの `Microsoft.Extensions.Logging` + Serilog の組み合わせが最も自然である。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

- ログに個人情報や機密情報は含まれない。ゲーム探索の統計データ（深さ、ノード数、評価値）のみが出力対象である。
- ログファイルの出力先は `./log/` ディレクトリであり、アプリケーションのローカル環境にのみ保存される。

### 7.2 スケーラビリティとパフォーマンス

- 探索ループ内でのロギング呼び出しがパフォーマンスに影響する可能性がある。ただし、現在のログ出力箇所は反復深化の各深さ完了時（1回/depth）のみであり、探索ノードごとではない。深さ 1〜16 で最大 16 回/探索であるため、パフォーマンスへの影響は無視できる。
- 将来、探索ノードごとの Debug ログを追加する場合は、`ILogger.IsEnabled(LogLevel.Debug)` によるガード条件を設けることで、ログレベルが Information 以上の場合にオーバーヘッドを回避する。

### 7.3 可観測性 (Observability)

- 本 RFC 自体がログ基盤の導入であり、可観測性の第一歩に位置づけられる。
- 構造化ログにより、`jq` やスクリプトによるフィルタリング・集計が可能になる。例: `jq 'select(.Properties.SearchProgress.Depth == 10)' reluca-20260205.json`

### 7.4 マイグレーションと後方互換性

- `PvsSearchEngine` のコンストラクタに `ILogger<PvsSearchEngine>` パラメータが追加されるため、既存のテストコードでコンストラクタ呼び出しの修正が必要になる。`NullLogger` を渡すことで動作への影響はない。
- コンソール出力のフォーマットが変更される。従来の `depth=8 nodes=154320 total=523100 value=12` 形式から Serilog のテンプレート形式に変わる。コンソール出力をパースしている外部ツール・スクリプトがある場合は対応が必要だが、現時点では該当なし。
- ロールバック手順: NuGet パッケージの削除と `Console.WriteLine` への復元で元に戻せる。破壊的変更はコンストラクタシグネチャのみ。

## 8. テスト戦略 (Test Strategy)

- **ユニットテスト**:
  - `PvsSearchEngine` の既存テスト全件が `NullLogger` 注入後も通過すること。
  - 探索結果（BestMove, Value, NodesSearched）が変更前と一致すること。
- **統合テスト**:
  - `DiProvider.Get()` から `PvsSearchEngine` を解決し、`ILogger<PvsSearchEngine>` が正しく注入されていることを確認する。
  - 実際の探索実行後に `./log/` 配下に JSON ログファイルが生成されることを確認する。
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
3. 既存テストの修正（`NullLogger` 注入）
4. 全テスト通過を確認

### フェーズ 3: 検証

1. JSON ログファイルの出力内容確認
2. `jq` によるパース・集計の動作確認
3. ローテーション動作の確認

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: 技術スタック表に `Serilog`（ログ基盤）を追加する。
- **`docs/domain-model.md`**: 影響なし（ドメインモデルの変更はない）。
