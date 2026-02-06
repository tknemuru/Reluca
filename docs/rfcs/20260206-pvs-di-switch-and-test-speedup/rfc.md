# [RFC] PvsSearchEngine への DI 切替・旧探索コード削除・テスト高速化

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
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

### 5.5 MSTest 並列実行の有効化

`Reluca.Tests` プロジェクトに `AssemblyInfo.cs` を追加する。

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]
```

**注意:** `ZobristTranspositionTable` が Singleton で DI 登録されているため、TT を使うテストクラス間で干渉する可能性がある。`PvsSearchEngine` は Transient だが TT は共有される。テストクラスごとに TT を Clear するか、テストの独立性を検証し、問題があれば `Workers` 数を制限する。

### 5.6 Search 系テストの探索深さ削減

以下の方針で depth を削減する。

| テストの目的 | 現在の depth | 変更後の depth |
|---|---|---|
| 有効な手を返す系 | 7 | 5 |
| ON/OFF 比較系（BestMove/Value 一致） | 7 | 5 |
| ノード数比較系（MPC, TT の効果） | 7〜10 | 5〜7 |
| 深さ別動作確認（depth=3, depth=5） | 3, 5 | 変更なし |

depth=10 のテスト `MPC_ON時のNodesSearchedがMPC_OFF時より少ない` は depth=7 に削減する。

### 5.7 Cachers ディレクトリの扱い

`Cachers/` ディレクトリ配下のファイルを全て削除した後、`ICacheable` インターフェースが他から参照されていなければディレクトリごと削除する。

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

案B を採用する。旧コードは `PvsSearchEngine` の完全な下位互換であり、残す理由がない。一括で行うことで中間状態（旧コードが残っている期間）を排除できる。テスト高速化も同時に行うことで、以降の開発サイクル全体が高速化する。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 スケーラビリティとパフォーマンス

`PvsSearchEngine` は `CachedNegaMax` と比較して以下の点で性能が向上する。

- **TT（置換表）**: 同一局面の再探索を回避
- **Aspiration Window**: 探索窓を狭めて枝刈り効率を向上
- **MPC（Multi-ProbCut）**: 浅い探索による統計的枝刈り
- **反復深化**: 浅い探索の結果を Move Ordering に活用

ただし、全オプション ON での本番動作は初めてとなるため、UI 対局での応答時間を検証する必要がある。

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
3. **UI 対局**: `PvsSearchEngine`（全オプション ON）で UI 対局が正常に動作すること
4. **並列実行の安全性**: MSTest 並列実行で TT の干渉によるテスト失敗が発生しないこと

## 9. 実装・リリース計画 (Implementation Plan)

### タスク一覧

| # | タスク | 依存 |
|---|---|---|
| 1 | DI 登録を `PvsSearchEngine` に変更 | - |
| 2 | `FindBestMover` で全オプション有効化 | 1 |
| 3 | 旧探索コード一式を削除（NegaMax / CachedNegaMax / NegaMaxTemplate / ISerchable / LegacySearchEngine） | 1 |
| 4 | Cacher 群を削除（MobilityCacher / EvalCacher / ReverseResultCacher）+ DiProvider から DI 登録削除 | 3 |
| 5 | `NegaMaxTest.cs` / `LegacySearchEngineUnitTest.cs` を削除 | 3 |
| 6 | `FindBestMover` 統合テストを `FindBestMoverUnitTest.cs` に移設 | 5 |
| 7 | MSTest 並列実行有効化（`AssemblyInfo.cs` 追加） | - |
| 8 | Search 系テストの depth 削減（7→5, 10→7） | - |
| 9 | 全テスト実行・実行時間計測 | 1-8 |
| 10 | `Cachers/` ディレクトリ・`ICacheable` の不要判断と削除 | 4 |

### 検証方法

- `dotnet test` で全テスト PASS を確認
- 実行時間が30秒以下であることを計測
- UI 対局で `PvsSearchEngine` が正常動作することを手動確認

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: Search セクションから `LegacySearchEngine` / `NegaMax` / `CachedNegaMax` の記述を削除。Cachers セクションから `MobilityCacher` / `EvalCacher` / `ReverseResultCacher` の記述を削除。レイヤー構造図から `LegacySearchEngine` / `NegaMax / CachedNegaMax` を削除
- **`docs/domain-model.md`**: 影響なし
