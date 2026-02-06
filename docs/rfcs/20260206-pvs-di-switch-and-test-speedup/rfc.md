# [RFC] PvsSearchEngine への DI 切替・旧探索コード削除・テスト高速化

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Accepted (承認済) |
| **作成日** | 2026-02-06 |
| **タグ** | Search, DI, Test, パフォーマンス |
| **関連リンク** | なし |

## 1. 要約 (Summary)

現在、`FindBestMover` が DI 経由で解決する `ISearchEngine` の実装は `LegacySearchEngine`（内部で `CachedNegaMax` を使用）であり、開発してきた `PvsSearchEngine`（TT・Aspiration Window・MPC）は本番で使われていない。DI 登録を `PvsSearchEngine` に切り替え、全オプション（TT / Aspiration Window / MPC）を有効化する。不要になった旧探索コード一式（`NegaMax` / `CachedNegaMax` / `NegaMaxTemplate` / `LegacySearchEngine` / `ISerchable` / Cacher 群）を削除する。併せて、テスト実行時間の短縮（MSTest 並列実行有効化・探索深さ削減・不要テスト削除）を行う。

## 2. 背景・動機 (Motivation)

### 本番で PvsSearchEngine が使われていない

`DiProvider.cs:122` で `ISearchEngine` の実装が `LegacySearchEngine` に登録されている。`FindBestMover` は `ISearchEngine` を DI で解決するため、UI 対局時は常に `LegacySearchEngine`（= `CachedNegaMax`）が動作する。`PvsSearchEngine` に実装した反復深化・TT・Aspiration Window・MPC は本番で一切活用されていない。

### 旧探索コードが残存している

`NegaMax` / `CachedNegaMax` / `NegaMaxTemplate` / `ISerchable` / `LegacySearchEngine` および `CachedNegaMax` 専用の Cacher 群（`MobilityCacher` / `EvalCacher` / `ReverseResultCacher`）が残存している。`PvsSearchEngine` が `ISearchEngine` の上位互換として機能する以上、これらは不要である。

### テスト実行に約3分かかる

`Reluca.Tests` の実行時間は約170秒である。主なボトルネックは以下の通り。

- `NegaMaxTest` の3テスト: **34秒**（本番未使用の `NegaMax` クラスのテスト）
- `LegacySearchEngineUnitTest` の `CachedNegaMax` 比較テスト2本: **各500ms**（旧実装ラッパーの回帰テスト）
- Search 系テストの大半が depth=7 で実行: 1テストあたり1〜19秒
- MSTest の並列実行が未設定: 全テストが直列実行

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

1. `FindBestMover` が `PvsSearchEngine` を使用するよう DI 登録を切り替え、TT / Aspiration Window / MPC を全て有効化する
2. 不要になった旧探索コード一式を削除する
3. テスト実行時間を170秒から30秒以下に短縮する

### やらないこと (Non-Goals)

- `PvsSearchEngine` のアルゴリズム変更や新機能追加
- `FindBestMover` の探索深さや終盤判定ロジックの変更
- `Reluca.Tools.Tests` の変更
- `ISearchEngine` インターフェース自体の変更

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- `PvsSearchEngine` は `ISearchEngine` インターフェースを既に実装済みである
- `PvsSearchEngine` の `SearchOptions` デフォルト値は全オプション OFF であり、明示的に ON にする必要がある
- Cacher 群（`MobilityCacher` / `EvalCacher` / `ReverseResultCacher`）は `CachedNegaMax` からのみ参照されており、他に依存者はいない

## 5. 詳細設計 (Detailed Design)

### 5.1 DI 登録の切替

`Reluca/Di/DiProvider.cs` を変更する。

```csharp
// 変更前 (DiProvider.cs:122)
services.AddTransient<ISearchEngine, LegacySearchEngine>();

// 変更後
services.AddTransient<ISearchEngine, PvsSearchEngine>();
```

### 5.2 FindBestMover でのオプション有効化

`Reluca/Movers/FindBestMover.cs` の `Move()` メソッドで `SearchOptions` を構築する際、全オプションを有効化する。

```csharp
// 変更前
var options = new SearchOptions(depth);

// 変更後
var options = new SearchOptions(
    depth,
    useTranspositionTable: true,
    useAspirationWindow: true,
    aspirationUseStageTable: true,
    useMultiProbCut: true
);
```

### 5.3 削除対象ファイル一覧

#### プロダクションコード

| ファイル | 理由 |
|---|---|
| `Reluca/Serchers/NegaMax.cs` | 本番未使用 |
| `Reluca/Serchers/NegaMaxTemplate.cs` | `NegaMax` / `CachedNegaMax` の基底クラス |
| `Reluca/Serchers/CachedNegaMax.cs` | `LegacySearchEngine` 経由のみ |
| `Reluca/Serchers/ISerchable.cs` | `NegaMaxTemplate` のみが実装 |
| `Reluca/Search/LegacySearchEngine.cs` | `CachedNegaMax` のラッパー |
| `Reluca/Cachers/MobilityCacher.cs` | `CachedNegaMax` 専用 |
| `Reluca/Cachers/EvalCacher.cs` | `CachedNegaMax` 専用 |
| `Reluca/Cachers/ReverseResultCacher.cs` | `CachedNegaMax` 専用 |

#### テストコード

| ファイル | 理由 |
|---|---|
| `Reluca.Tests/Serchers/NegaMaxTest.cs` | 削除対象クラスのテスト |
| `Reluca.Tests/Search/LegacySearchEngineUnitTest.cs` | 削除対象クラスのテスト |

#### DI 登録の削除（DiProvider.cs）

```csharp
// 以下の行を削除
services.AddSingleton<MobilityCacher, MobilityCacher>();
services.AddSingleton<EvalCacher, EvalCacher>();
services.AddSingleton<ReverseResultCacher, ReverseResultCacher>();
services.AddTransient<NegaMax, NegaMax>();
services.AddTransient<CachedNegaMax, CachedNegaMax>();
```

### 5.4 LegacySearchEngineUnitTest から残すテスト

`LegacySearchEngineUnitTest` は全体を削除するが、以下のテストは `FindBestMover` の統合テストとして価値があるため、新たに `FindBestMoverUnitTest.cs` へ移設する。

- `FindBestMover経由で終盤探索が正常に動作する`

移設先テストの DI 構成は `DiProvider` の標準構成をそのまま使用する。DI 切替後は `DiProvider.Get().GetService<ISearchEngine>()` が `PvsSearchEngine` を返すため、`FindBestMover` は自動的に `PvsSearchEngine` 経由で探索を実行する。テストコードの変更は DI 解決部分のみであり、テストの意図（`FindBestMover` 経由で終盤局面に対して有効な手が返ること）は維持される。

### 5.5 MSTest 並列実行の有効化

`Reluca.Tests` プロジェクトに `AssemblyInfo.cs` を追加する。

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]
```

#### TT 共有に対する緩和策

`ZobristTranspositionTable` が Singleton で DI 登録されているため、TT を使うテストクラスが並列実行されると、テスト間で TT の内容が干渉する可能性がある。

**採用する緩和策**: `PvsSearchEngine.Search()` は探索開始時に `_transpositionTable.Clear()` を呼び出しており、各探索の開始時点で TT はクリアされる。ただし、並列実行中に他のテストクラスが同時に TT へ Store/Probe を行うと非決定的な結果を招く恐れがある。これを防ぐため、TT を使用するテストクラスに `[DoNotParallelize]` 属性を付与し、TT 使用テスト同士が並列実行されないようにする。具体的には以下のテストクラスが対象である。

- `PvsSearchEngineWithTTUnitTest`
- `PvsSearchEngineAspirationWindowUnitTest`（TT ON のテストを含む）
- `PvsSearchEngineAspirationTuningUnitTest`（TT ON のテストを含む）
- `PvsSearchEngineMpcUnitTest`（TT ON のテストを含む）
- `FindBestMoverUnitTest`（`FindBestMover` が全オプション ON で TT を使用する）

TT を使わないテストクラス（`PvsSearchEngineIterativeDeepeningUnitTest` の TT OFF テスト等）は並列実行の恩恵を受ける。

#### 将来の TT 使用テストクラス追加時の運用ルール

TT を使用する新規テストクラスを追加する際は、必ず `[DoNotParallelize]` 属性を付与すること。付与漏れを防ぐため、TT を使用するテストクラスの命名には `WithTT` を含める命名規約を採用する（例: `PvsSearchEngineWithTTUnitTest`）。コードレビュー時に TT 使用の有無と `[DoNotParallelize]` の付与状況を確認項目とする。

### 5.6 Search 系テストの探索深さ削減

以下の方針で depth を削減する。

| テストの目的 | 現在の depth | 変更後の depth |
|---|---|---|
| 有効な手を返す系 | 7 | 5 |
| ON/OFF 比較系（BestMove/Value 一致） | 7 | 5 |
| ノード数比較系（MPC, TT の効果） | 7〜10 | 5〜7 |
| 深さ別動作確認（depth=3, depth=5） | 3, 5 | 変更なし |

depth=10 のテスト `MPC_ON時のNodesSearchedがMPC_OFF時より少ない` は depth=7 に削減する。

#### depth 削減に伴うアサーション更新方針

depth を変更すると探索結果が変わるため、既存のアサーション値（期待される最善手、ノード数の比較条件等）が成立しなくなる可能性がある。以下の方針で対応する。

- **有効な手を返す系**: アサーションは「合法手の集合に含まれること」であるため、depth 変更の影響を受けない。更新不要である。
- **ON/OFF 比較系（BestMove/Value 一致）**: depth=5 でも ON/OFF で同一結果が返ることを実測で確認する。不一致の場合は以下の順序で対応する。
  1. depth=5 で BestMove/Value が一致する別の局面（テスト盤面）を探索し、テストを再構成する。
  2. 適切な局面が見つからない場合は、テストの検証観点を「BestMove の一致」から「両方が合法手であること」に変更する。値の一致テストが本質的に必要な場合は depth を据え置くことも許容する。
- **ノード数比較系**: アサーションは「ON 時のノード数 < OFF 時のノード数」という相対比較であるため、depth 変更の影響を受けにくい。depth=5〜7 でも枝刈り効果の大小関係が維持されることを実測で確認する。

### 5.7 Cachers ディレクトリの扱い

`Cachers/` ディレクトリ配下の `MobilityCacher.cs` / `EvalCacher.cs` / `ReverseResultCacher.cs` を削除する。`ICacheable<TKey, TValue>` インターフェース（`Cachers/ICacheable.cs`）はこれら3つの Cacher クラスからのみ実装されており、他に参照元は存在しない（コードベース調査で確認済み）。したがって `ICacheable.cs` も削除し、`Cachers/` ディレクトリごと削除する。

## 6. 代替案の検討 (Alternatives Considered)

### 案A: DI 切替のみ行い、旧コードは残す

- **概要**: DI 登録を `PvsSearchEngine` に変更するが、`LegacySearchEngine` / `CachedNegaMax` 等は削除せずコードベースに残す
- **長所**: 変更範囲が小さく、万が一の切り戻しが容易
- **短所**: 本番で使われないコードが残存し、メンテナンスコストとなる。新規開発者の混乱を招く。テスト実行時間の改善が限定的（NegaMaxTest 34秒が残る）

### 案B: DI 切替 + 旧コード削除 + テスト高速化を一括実施（採用案）

- **概要**: DI 切替、旧コード一式の削除、テスト並列化・depth 削減を一括で行う
- **長所**: 不要コード完全除去によりコードベースが簡潔になる。テスト実行時間が170秒→30秒以下に短縮。開発サイクルの高速化
- **短所**: 変更範囲が広い。ただし削除対象は明確に特定済みであり、`PvsSearchEngine` が `ISearchEngine` の上位互換である点も検証済み

### 選定理由

案B を採用する。旧コードは `PvsSearchEngine` の完全な下位互換であり、残す理由がない。一括で行うことで中間状態（旧コードが残っている期間）を排除できる。テスト高速化も同時に行うことで、以降の開発サイクル全体が高速化する。問題発生時の切り戻しは Git revert で対応可能であり、タスク間にゲート条件を設けることでリスクを緩和する（9章 実装計画を参照）。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

`PvsSearchEngine` は `CachedNegaMax` と比較して以下の点で性能が向上する。

- **TT（置換表）**: 同一局面の再探索を回避
- **Aspiration Window**: 探索窓を狭めて枝刈り効率を向上
- **MPC（Multi-ProbCut）**: 浅い探索による統計的枝刈り
- **反復深化**: 浅い探索の結果を Move Ordering に活用

ただし、全オプション ON での本番動作は初めてとなるため、UI 対局での応答時間を検証する必要がある。

#### 応答時間が許容範囲を超えた場合の緩和策

UI 対局で1手あたりの応答時間が 10 秒を超える場合、以下の順序でオプションを段階的に無効化して原因を特定し、許容範囲内に収める。

1. `useMultiProbCut: false` に変更（MPC の統計的枝刈りが逆効果の場合）
2. `useAspirationWindow: false` に変更（Aspiration Window の fail-high/low 再探索が過多の場合）
3. `aspirationUseStageTable: false` に変更（ステージ別テーブルによる初期窓が不適切な場合）

各段階で応答時間を再計測し、許容範囲内に収まった時点で確定する。TT は常に有効とする（無効化すると性能が大幅に劣化するため）。

### 7.2 マイグレーションと後方互換性

- `ISearchEngine` インターフェースは変更しないため、外部から見た互換性は維持される
- `FindBestMover` の `Move()` メソッドの戻り値型・シグネチャは変更しない
- DI 切替により探索結果（最善手の選択）が変わる可能性がある。これはアルゴリズムの違いによるものであり、いずれも正当な手を返す

## 8. テスト戦略 (Test Strategy)

### 削除するテスト

- `NegaMaxTest.cs`: 削除対象クラスのテスト。3テスト（34秒）
- `LegacySearchEngineUnitTest.cs`: 削除対象クラスのテスト。ただし `FindBestMover` 統合テストは移設

### 移設するテスト

- `FindBestMover経由で終盤探索が正常に動作する` → `FindBestMoverUnitTest.cs` へ移設

### 変更するテスト

- Search 系テストの depth を 7→5 に削減（ノード数比較系は 7 に据え置き可）
- depth=10 のテストを depth=7 に削減

### 検証観点

1. **全テスト PASS**: depth 削減後も全テストが PASS すること
2. **テスト実行時間**: 170秒→30秒以下に短縮されていること
3. **UI 対局の動作検証**: `PvsSearchEngine`（全オプション ON）で UI 対局を実施し、以下の基準を全て満たすこと
   - 合法手が返される（盤面上の有効な位置に着手できる）
   - 1手あたりの応答時間が 10 秒以内である（depth=7 の中盤局面で計測）
   - 対局を最後まで完走できる（途中で例外やフリーズが発生しない）
4. **並列実行の安全性**: MSTest 並列実行で TT の干渉によるテスト失敗が発生しないこと

## 9. 実装・リリース計画 (Implementation Plan)

### タスク一覧

| # | タスク | 依存 | ゲート条件 |
|---|---|---|---|
| 1 | DI 登録を `PvsSearchEngine` に変更 | - | - |
| 2 | `FindBestMover` で全オプション有効化 | 1 | ビルド成功・全テスト PASS |
| 3 | 旧探索コード一式を削除（NegaMax / CachedNegaMax / NegaMaxTemplate / ISerchable / LegacySearchEngine） | 2 | ビルド成功・全テスト PASS |
| 4 | Cacher 群を削除（MobilityCacher / EvalCacher / ReverseResultCacher / ICacheable）+ DiProvider から DI 登録削除 | 3 | ビルド成功 |
| 5 | `NegaMaxTest.cs` / `LegacySearchEngineUnitTest.cs` を削除 | 3 | - |
| 6 | `FindBestMover` 統合テストを `FindBestMoverUnitTest.cs` に移設 | 5 | ビルド成功・全テスト PASS |
| 7 | MSTest 並列実行有効化（`AssemblyInfo.cs` 追加）+ TT 使用テストクラスに `[DoNotParallelize]` 付与 | - | - |
| 8 | Search 系テストの depth 削減（7→5, 10→7）+ アサーション値の実測更新 | - | - |
| 9 | 全テスト実行・実行時間計測 | 1-8 | 全テスト PASS・実行時間 30 秒以下 |
| 10 | `Cachers/` ディレクトリ削除 | 4 | ビルド成功 |

### ゲート条件と切り戻し方針

タスク間にゲート条件を設け、各段階で品質を確認する。ゲート条件を満たさない場合は、直前のコミットに `git revert` で切り戻す。

- **タスク 2 完了後**: `dotnet build` が成功し、`dotnet test` で旧コードのテスト（`NegaMaxTest` 等）を含む全テストが PASS することを確認する。この時点では旧コード・旧テストがまだ存在するため、DI 切替後も旧テストが PASS することで後方互換性を検証する。失敗した場合、DI 切替またはオプション有効化に問題がある。タスク 1-2 のコミットを revert する。
- **タスク 3 完了後**: `dotnet build` が成功し、`dotnet test` で全テスト PASS を確認する。ビルドエラーが発生した場合、削除対象外のコードから旧コードへの参照が残っている。参照を調査し、削除範囲を修正する。
- **タスク 6 完了後**: `dotnet build` が成功し、`dotnet test` で移設した `FindBestMoverUnitTest` を含む全テストが PASS することを確認する。旧テスト削除とテスト移設が正しく行われたことを検証する。失敗した場合は移設テストの DI 構成やテストコードを修正する。
- **タスク 9 完了後**: 全テスト PASS かつ実行時間 30 秒以下を確認する。実行時間が目標を超える場合、depth 値または並列設定を調整する。

### 検証方法

- `dotnet test` で全テスト PASS を確認
- 実行時間が30秒以下であることを計測
- UI 対局で以下を手動確認: 合法手が返される、1手あたり 10 秒以内、対局を完走できる

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: Search セクションから `LegacySearchEngine` / `NegaMax` / `CachedNegaMax` の記述を削除。Cachers セクションから `MobilityCacher` / `EvalCacher` / `ReverseResultCacher` の記述を削除。レイヤー構造図から `LegacySearchEngine` / `NegaMax / CachedNegaMax` を削除
- **`docs/domain-model.md`**: 影響なし
