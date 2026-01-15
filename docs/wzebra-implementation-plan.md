# WZebra アルゴリズム導入 Plan（Task 1 / Task 2）

## 1) Architecture Overview

### 現状アーキテクチャの要約

#### 主要コンポーネント

| コンポーネント | ファイルパス | 責務 |
|-------------|------------|------|
| **FindBestMover** | `Reluca/Movers/FindBestMover.cs` | 探索の入口。CachedNegaMax を呼び出し、終盤では評価関数を切り替える |
| **NegaMaxTemplate** | `Reluca/Serchers/NegaMaxTemplate.cs` | 抽象基底クラス。Template Method パターンで NegaMax 探索の骨格を定義 |
| **CachedNegaMax** | `Reluca/Serchers/CachedNegaMax.cs` | NegaMaxTemplate を継承。3種のキャッシャーを統合した推奨探索実装 |
| **FeaturePatternEvaluator** | `Reluca/Evaluates/FeaturePatternEvaluator.cs` | パターンベース評価関数（Logistello 模倣） |
| **MobilityAnalyzer** | `Reluca/Analyzers/MobilityAnalyzer.cs` | 着手可能位置の列挙 |
| **MoveAndReverseUpdater** | `Reluca/Updaters/MoveAndReverseUpdater.cs` | 石の反転処理 |
| **EvalCacher** | `Reluca/Cachers/EvalCacher.cs` | 評価値キャッシュ（キー: `Turn|Black|White`） |
| **MobilityCacher** | `Reluca/Cachers/MobilityCacher.cs` | 着手可能位置キャッシュ |
| **ReverseResultCacher** | `Reluca/Cachers/ReverseResultCacher.cs` | 反転結果キャッシュ |
| **DiProvider** | `Reluca/Di/DiProvider.cs` | DI コンテナ（Microsoft.Extensions.DI） |

#### 依存方向

```
┌───────────────────────────────────────────────────────┐
│  UI Layer (WinForms / Console)                        │
└───────────────────┬───────────────────────────────────┘
                    │ IMovable.Move()
                    ▼
┌───────────────────────────────────────────────────────┐
│  FindBestMover                                        │
│  - CachedNegaMax を生成・呼び出し                     │
│  - 終盤で Evaluator / LimitDepth を切り替え           │
└───────────────────┬───────────────────────────────────┘
                    │ ISerchable.Search()
                    ▼
┌───────────────────────────────────────────────────────┐
│  CachedNegaMax : NegaMaxTemplate                      │
│  (Template Method パターン)                           │
├───────────────────────────────────────────────────────┤
│  依存:                                                │
│  - IEvaluable (FeaturePatternEvaluator / DiscCount)  │
│  - MobilityAnalyzer                                   │
│  - MoveAndReverseUpdater                              │
│  - EvalCacher / MobilityCacher / ReverseResultCacher │
└───────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────┐
│  Data Layer                                           │
│  - GameContext, BoardContext, Disc                   │
│  - BoardAccessor (static ヘルパー)                    │
└───────────────────────────────────────────────────────┘
```

#### 探索の入口

- **FindBestMover.Move()** が公開 API
- 内部で `DiProvider.Get().GetService<CachedNegaMax>()` を取得
- `CachedNegaMax.Search()` → `NegaMaxTemplate.SearchBestValue()` で再帰探索

### WZebra 要素の差し込み位置

| WZebra 要素 | 差し込み層 | 理由 |
|------------|----------|------|
| **PVS (NegaScout)** | 新探索コア | Null Window Search の導入。NegaMaxTemplate とは別構造で実装 |
| **反復深化 (ID)** | 新探索コア | 浅い探索結果を次の深度に継承。探索制御ループとして新設 |
| **置換表 (TT)** | 新探索専用層 | 探索中の局面情報（depth/value/boundType/bestMove）を保持。EvalCacher とは別物 |
| **MPC** | 新探索コア内 | 統計的枝刈り。PVS/ID が前提となるため、新探索コアに組み込む |

### 採用する責務分割と依存方向

```
┌───────────────────────────────────────────────────────┐
│  FindBestMover (終盤切替ロジックは維持)               │
│  - ISearchEngine インターフェース経由で探索を呼ぶ     │
│  - 終盤 Evaluator 切替は従来通りここで行う           │
└───────────────────┬───────────────────────────────────┘
                    │ ISearchEngine.Search()
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│  ISearchEngine (新規インターフェース)                               │
│  └── SearchResult Search(GameContext context, SearchOptions opts)  │
├─────────────────────────────────────────────────────────────────────┤
│  実装A: LegacySearchEngine (既存 CachedNegaMax をラップ)           │
│  実装B: PvsSearchEngine (新規: PVS/ID/TT/MPC) ← 後続タスク         │
└─────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────┐
│  ITranspositionTable (新規インターフェース)           │
│  └── 実装: ZobristTranspositionTable                 │
├───────────────────────────────────────────────────────┤
│  配置: Reluca/Search/Transposition/                  │
│  役割: 探索中の局面キャッシュ (depth/value/bound/move)│
│  EvalCacher とは独立。探索コア専用                   │
└───────────────────────────────────────────────────────┘
```

**理由:**
- **ISearchEngine による抽象化**: 既存 NegaMaxTemplate を壊さず、新旧探索を FeatureFlag や DI で切り替え可能
- **TT と EvalCacher の分離**: EvalCacher は「評価関数の計算結果」のキャッシュ。TT は「探索ノード情報（depth/bound/bestMove 含む）」のキャッシュ。責務が異なる
- **TT の配置場所**: `Reluca/Cachers/` は汎用キャッシュ層。TT は探索専用のため `Reluca/Search/Transposition/` に配置
- **Template Method 不使用**: 新探索コアは明示的な制御フローを持つ。探索実験・パラメータ変更・MPC 組み込みが容易

---

## 2) Options Considered

### 案A（採用案）: ISearchEngine + 明示的制御フロー

**構成:**
- 新規 `ISearchEngine` インターフェースを導入
- 既存 `CachedNegaMax` は `LegacySearchEngine` としてラップ（後方互換）
- 新探索コア `PvsSearchEngine` は Template Method を使わず、明示的なループ/再帰で PVS/ID/TT を実装
- `ITranspositionTable` を独立したインターフェースとして定義
- 終盤 Evaluator 切替ロジックは FindBestMover に残す（挙動変更を避ける）

**メリット:**
- 既存コードへの影響が最小限（FindBestMover の変更は ISearchEngine 呼び出しへの差し替えのみ）
- 探索アルゴリズムの実験・比較が容易（DI で切り替え可能）
- TT のエントリ構造（depth/boundType/bestMove）を探索コアが直接制御できる
- 将来の MPC 導入時、浅い探索結果の取得・回帰パラメータ適用が明示的に記述可能

**デメリット:**
- 新規コード量が多い（ISearchEngine + PvsSearchEngine + ITranspositionTable）
- 新規ディレクトリ `Reluca/Search/` の追加

### 案B（不採用案）: NegaMaxTemplate を継承拡張

**構成:**
- `NegaMaxTemplate` を継承した `PvsNegaMax` クラスを作成
- TT を EvalCacher と同様のパターンで `Reluca/Cachers/` に追加
- SearchBestValue() をオーバーライドして PVS ロジックを埋め込む

**メリット:**
- 既存コードとの親和性が高い（継承パターンを維持）
- 変更範囲が比較的小さい

**デメリット:**
- **Template Method の制約**: `SearchBestValue()` の骨格が固定されており、以下の実装が困難
  - 反復深化ループの導入（外側のループを入れる場所がない）
  - Null Window Search の分岐（広い窓と狭い窓の切り替え）
  - MPC の統計的枝刈り（浅い探索を「途中で」呼ぶ制御が複雑）
- **抽象メソッドの増殖**: 新機能を入れるたびに abstract メソッドが増え、既存実装にも影響
- **テスト困難**: 探索ロジックが分散し、ユニットテストの粒度が粗くなる

### 置換表 (TT) と既存 EvalCacher の役割の違い

| 観点 | EvalCacher | Transposition Table (TT) |
|-----|-----------|--------------------------|
| **キャッシュ対象** | 評価関数の計算結果 | 探索ノード情報 |
| **キー** | `Turn|Black|White`（文字列） | Zobrist Hash（64bit 整数） |
| **格納値** | `long` (評価値のみ) | `TTEntry` (depth, value, boundType, bestMove) |
| **用途** | 同一盤面の評価計算を省略 | 探索済み局面の再利用、手順序ヒント取得 |
| **boundType** | なし | Exact / LowerBound / UpperBound |
| **depth 情報** | なし | あり（浅い探索結果は深い探索で再利用不可） |
| **bestMove** | なし | あり（反復深化で最善手を次の深度に継承） |
| **配置** | `Reluca/Cachers/` | `Reluca/Search/Transposition/` |

**結論:** EvalCacher は「葉ノードの評価値」のキャッシュであり、TT は「探索ノードの探索結果」のキャッシュ。両者は目的・構造が異なるため、別コンポーネント・別ディレクトリとして設計する。

### Template Method を使い続けた場合の問題点

1. **反復深化の導入困難**: `Search()` → `SearchBestValue()` の単純な呼び出し構造では、深さ 1→2→3... のループを外側に挿入する場所がない
2. **Null Window Search の制御困難**: `SearchBestValue()` 内で `alpha`, `beta` を動的に変更する「再探索」ロジックが、Template の骨格に収まらない
3. **MPC の浅い探索呼び出し**: `SearchBestValue()` の再帰中に「別の深度で同じ局面を探索」する必要があり、Template の再帰構造と競合
4. **テストの困難さ**: ロジックが abstract メソッドに分散し、探索アルゴリズム単体のテストが書きにくい

---

## 3) Execution Plan for AI Tools

### Epic

既存の Reluca オセロ AI を WZebra アーキテクチャに段階的にアップグレードする。第一段階として、探索エンジンの差し替え可能化（Task 1）と置換表の導入（Task 2）を実施し、将来の PVS / 反復深化 / MPC 実装に向けた基盤を構築する。既存の WinForms / Console 動作を維持しつつ、FeatureFlag による探索エンジンの切り替えを可能にする。

---

### Task 1: 探索エンジンの差し替え可能化（入口の安定）

#### 目的

- 探索エンジンを抽象化し、新旧実装を DI / FeatureFlag で切り替え可能にする
- 既存 `NegaMaxTemplate` / `CachedNegaMax` を後方互換として維持
- **終盤 Evaluator 切替ロジックは FindBestMover に残し、挙動変更を避ける**
- 将来の PVS / ID / MPC 導入に向けた拡張ポイントを確立

#### 期待成果物

1. `ISearchEngine` インターフェース
2. `SearchResult` 結果クラス（探索結果の手・評価値）
3. `SearchOptions` オプションクラス（最小構成: 深さ制限のみ）
4. `LegacySearchEngine` クラス（既存 CachedNegaMax のラッパー）
5. `FindBestMover` の修正（ISearchEngine 経由での呼び出し、終盤ロジックは維持）
6. DI 登録の追加
7. ユニットテスト

#### 変更範囲

| 種別 | ファイル / ディレクトリ |
|-----|----------------------|
| 新規作成 | `Reluca/Search/ISearchEngine.cs` |
| 新規作成 | `Reluca/Search/SearchResult.cs` |
| 新規作成 | `Reluca/Search/SearchOptions.cs` |
| 新規作成 | `Reluca/Search/LegacySearchEngine.cs` |
| 修正 | `Reluca/Movers/FindBestMover.cs` |
| 修正 | `Reluca/Di/DiProvider.cs` |
| 新規作成 | `Reluca.Tests/Search/LegacySearchEngineUnitTest.cs` |

#### 手順

1. **ディレクトリ作成**
   - `Reluca/Search/` ディレクトリを新規作成

2. **ISearchEngine インターフェース定義**
   - `SearchResult Search(GameContext context, SearchOptions options)` メソッド
   - 日本語 Doc コメント必須
   - ModuleDoc: 責務・入出力・副作用を記載

3. **SearchResult クラス作成**
   - `BestMove` (int): 最善手
   - `Value` (long): 評価値
   - 推測: 将来的に `NodesSearched`, `Depth` 等を追加する可能性あり

4. **SearchOptions クラス作成（最小構成）**
   - `MaxDepth` (int): 深さ制限
   - TT サイズ等の設定は TT 側（DI / コンストラクタ）に寄せる

5. **LegacySearchEngine 実装**
   - `CachedNegaMax` を内部で利用
   - `ISearchEngine.Search()` を実装
   - **終盤 Evaluator 切替は行わない**（FindBestMover の責務のまま）
   - `SearchOptions.MaxDepth` を `CachedNegaMax.Initialize()` に渡す

6. **FindBestMover 修正**
   - `ISearchEngine` を DI で注入
   - `Move()` 内で `ISearchEngine.Search()` を呼ぶ
   - **終盤 Evaluator 切替ロジック（TurnCount >= 46）は従来通りここで行う**
   - 既存の `CachedNegaMax` 直接参照を削除

7. **DiProvider 修正**
   - `ISearchEngine` → `LegacySearchEngine` のバインディング追加
   - `LegacySearchEngine` を Transient で登録

8. **ユニットテスト作成**
   - `LegacySearchEngine` が既存 `CachedNegaMax` と同一結果を返すことを検証
   - 回帰テスト: 既存テストケースがすべてパスすることを確認

#### DoD（完了条件）

- [ ] `ISearchEngine` / `SearchResult` / `SearchOptions` が定義されている
- [ ] `LegacySearchEngine` が `CachedNegaMax` をラップしている
- [ ] `FindBestMover` が `ISearchEngine` 経由で探索を呼んでいる
- [ ] **終盤 Evaluator 切替ロジックは FindBestMover に残っている**
- [ ] `dotnet build` が成功する
- [ ] `dotnet test` が全件パスする（既存テスト含む）
- [ ] 日本語 Doc コメントが全クラス・全メソッドに記載されている
- [ ] ModuleDoc（責務・入出力・副作用）が各ファイル冒頭にある

#### Verify

```bash
# リポジトリルートで実行
dotnet build
dotnet test
```

#### Rollback

```bash
# Task 1 のコミットを特定し、revert する
git log --oneline -10
git revert <commit-hash>
```

- 新規ディレクトリ `Reluca/Search/` を削除
- FindBestMover.cs / DiProvider.cs は Task 1 開始前の状態に戻す

#### リスク・注意点

1. **DI ライフサイクル**: `LegacySearchEngine` を Singleton にすると、内部の `CachedNegaMax` 状態が残る可能性。Transient 推奨
2. **終盤切替ロジックの重複回避**: `LegacySearchEngine` 側で Evaluator 切替を行わないこと。FindBestMover との二重切替を防ぐ
3. **既存テストへの影響**: `FindBestMover` の内部実装変更により、モック設定が必要なテストがあれば修正

---

### Task 2: 置換表（Transposition Table）導入

#### 目的

- Zobrist Hashing による高速な局面識別を導入
- 探索ノード情報（depth / value / boundType / bestMove）を格納する置換表を構築
- FeatureFlag で ON/OFF 可能な設計とし、既存動作への影響を最小化
- TT 関連ファイルは探索専用ディレクトリ `Reluca/Search/Transposition/` に配置

#### 期待成果物

1. `ZobristHash` クラス（盤面からハッシュ値を生成）
2. `ZobristKeys` 静的クラス（乱数テーブル）
3. `TTEntry` 構造体（TT エントリ）
4. `BoundType` 列挙型
5. `ITranspositionTable` インターフェース
6. `ZobristTranspositionTable` 実装クラス
7. `TranspositionTableConfig` 設定クラス（サイズ等）
8. DI 登録
9. ユニットテスト

#### 変更範囲

| 種別 | ファイル / ディレクトリ |
|-----|----------------------|
| 新規作成 | `Reluca/Search/Transposition/ZobristHash.cs` |
| 新規作成 | `Reluca/Search/Transposition/ZobristKeys.cs` |
| 新規作成 | `Reluca/Search/Transposition/TTEntry.cs` |
| 新規作成 | `Reluca/Search/Transposition/BoundType.cs` |
| 新規作成 | `Reluca/Search/Transposition/ITranspositionTable.cs` |
| 新規作成 | `Reluca/Search/Transposition/ZobristTranspositionTable.cs` |
| 新規作成 | `Reluca/Search/Transposition/TranspositionTableConfig.cs` |
| 修正 | `Reluca/Di/DiProvider.cs` |
| 新規作成 | `Reluca.Tests/Search/Transposition/ZobristHashUnitTest.cs` |
| 新規作成 | `Reluca.Tests/Search/Transposition/ZobristTranspositionTableUnitTest.cs` |

#### 手順

1. **ディレクトリ作成**
   - `Reluca/Search/Transposition/` ディレクトリを新規作成
   - `Reluca.Tests/Search/Transposition/` ディレクトリを新規作成

2. **ZobristKeys 静的クラス作成**
   - 64 マス × 2 色（黒/白）× 乱数テーブル = 128 個の `ulong` 乱数
   - ターン用乱数 1 個
   - 乱数は固定シードで生成（再現性確保）
   - ModuleDoc 必須

3. **ZobristHash クラス作成**
   - `ulong ComputeHash(GameContext context)`: 盤面からハッシュ値を計算
   - 推測: 将来的に `UpdateHash()` による差分更新を追加する可能性あり
   - ModuleDoc 必須

4. **BoundType 列挙型作成**
   ```
   Exact = 0,      // 正確な値
   LowerBound = 1, // α カット（この値以上）
   UpperBound = 2  // β カット（この値以下）
   ```

5. **TTEntry 構造体作成**
   - `ulong Key`: Zobrist ハッシュ値（衝突検出用）
   - `int Depth`: 探索深さ
   - `long Value`: 評価値
   - `BoundType Bound`: 境界タイプ
   - `int BestMove`: 最善手（-1 は未設定）

6. **TranspositionTableConfig クラス作成**
   - `int Size`: テーブルサイズ（デフォルト: 2^20 = 約100万エントリ）
   - 推測: 将来的に置換ポリシー設定等を追加する可能性あり
   - TT サイズの設定はここに集約（SearchOptions には含めない）

7. **ITranspositionTable インターフェース定義**
   - `void Store(ulong key, int depth, long value, BoundType bound, int bestMove)`
   - `bool TryProbe(ulong key, int depth, long alpha, long beta, out TTEntry entry)`
   - `void Clear()`
   - `int GetBestMove(ulong key)`: 手順序ヒント取得用
   - ModuleDoc 必須

8. **ZobristTranspositionTable 実装**
   - コンストラクタで `TranspositionTableConfig` を受け取る
   - 内部配列: `TTEntry[]`
   - インデックス: `key & (size - 1)`
   - 衝突時の置換戦略: Depth-Preferred（より深い探索結果を優先）
   - `TryProbe()` では depth 条件と bound 条件をチェック
   - ModuleDoc 必須

9. **DiProvider 修正**
   - `TranspositionTableConfig` を Singleton で登録
   - `ITranspositionTable` → `ZobristTranspositionTable` のバインディング追加
   - `ZobristTranspositionTable` を Singleton 登録（探索セッション中は同一インスタンスを使用）

10. **ユニットテスト作成**
    - `ZobristHashUnitTest`: 同一盤面 → 同一ハッシュ、異なる盤面 → 異なるハッシュ
    - `ZobristTranspositionTableUnitTest`: Store/Probe の基本動作、衝突時の置換ポリシー

#### DoD（完了条件）

- [ ] `ZobristHash` が盤面からハッシュ値を生成できる
- [ ] `TTEntry` が depth/value/boundType/bestMove を保持している
- [ ] `ITranspositionTable` の Store/TryProbe が実装されている
- [ ] TT サイズは `TranspositionTableConfig` で設定可能
- [ ] `dotnet build` が成功する
- [ ] `dotnet test` が全件パスする
- [ ] 日本語 Doc コメントが全クラス・全メソッドに記載されている
- [ ] ModuleDoc が各ファイル冒頭にある

#### Verify

```bash
# リポジトリルートで実行
dotnet build
dotnet test

# TT 単体テスト（フィルタ指定）
dotnet test --filter "FullyQualifiedName~ZobristTranspositionTable"

# Zobrist ハッシュ単体テスト（フィルタ指定）
dotnet test --filter "FullyQualifiedName~ZobristHash"
```

#### Rollback

```bash
git log --oneline -10
git revert <commit-hash>
```

- 新規ディレクトリ `Reluca/Search/Transposition/` を削除
- `Reluca.Tests/Search/Transposition/` を削除
- DiProvider.cs の TT 関連バインディングを削除

#### リスク・注意点

1. **ハッシュ衝突**: Zobrist は衝突可能。TTEntry に元の Key を保持し、Probe 時に検証することで誤判定を防ぐ
2. **メモリ使用量**: 100万エントリ × 約32バイト = 約32MB。許容範囲だが、`TranspositionTableConfig` でサイズ調整可能
3. **TT と EvalCacher の混同**: TT は探索ノード情報、EvalCacher は評価値のみ。配置ディレクトリも分離（`Search/Transposition/` vs `Cachers/`）
4. **探索コアとの統合**: Task 2 では TT の「箱」のみ作成。実際の探索コアへの組み込みは後続タスク（PvsSearchEngine 実装時）

---

## 4) Quality Gates

### テスト方針

#### 何をテストするか

| 対象 | テスト内容 |
|-----|----------|
| **ZobristHash** | 同一盤面で同一ハッシュ、異なる盤面で異なるハッシュ、ターン違いで異なるハッシュ |
| **ZobristTranspositionTable** | Store → Probe で正しく取得、depth 条件による Probe 失敗、衝突時の置換ポリシー |
| **TTEntry** | 値の保持・取得、デフォルト値 |
| **LegacySearchEngine** | 既存 CachedNegaMax と同一結果を返す回帰テスト |
| **ISearchEngine 経由の探索** | FindBestMover からの呼び出しで正常動作 |

#### 何をテストしないか（理由付き）

| 対象 | 理由 |
|-----|------|
| **PvsSearchEngine の探索ロジック** | Task 1/2 のスコープ外。後続タスクでテスト |
| **TT を使った探索の性能改善** | TT 統合は後続タスク。本タスクは「箱」の作成のみ |
| **UI (WinForms) の動作** | 手動確認で十分。自動テストのコスト対効果が低い |
| **MPC の統計的枝刈り** | スコープ外 |

### 最小テストセット

| ID | テストクラス | テスト名 | 検証内容 |
|----|------------|---------|---------|
| T1-1 | `ZobristHashUnitTest` | `同一盤面で同一ハッシュを返す` | Hash 一意性 |
| T1-2 | `ZobristHashUnitTest` | `異なる盤面で異なるハッシュを返す` | Hash 分散性 |
| T1-3 | `ZobristHashUnitTest` | `ターン違いで異なるハッシュを返す` | ターン考慮 |
| T2-1 | `ZobristTranspositionTableUnitTest` | `Storeした値をProbeで取得できる` | 基本動作 |
| T2-2 | `ZobristTranspositionTableUnitTest` | `depth条件を満たさない場合Probeは失敗する` | depth フィルタ |
| T2-3 | `ZobristTranspositionTableUnitTest` | `衝突時に深い探索結果が優先される` | 置換ポリシー |
| T2-4 | `ZobristTranspositionTableUnitTest` | `Clearで全エントリが削除される` | クリア動作 |
| T3-1 | `LegacySearchEngineUnitTest` | `既存探索と同一結果を返す` | 回帰テスト |
| T3-2 | `LegacySearchEngineUnitTest` | `終盤でも探索結果が正しい` | 終盤動作（切替は FindBestMover 側） |

### Doc コメント方針

- **すべて日本語**（英語禁止）
- **public / private 問わず**全メソッドに Doc コメント
- **ModuleDoc**: 各ファイル冒頭に責務・入出力・副作用を 3 行程度で記載

```csharp
/// <summary>
/// 【ModuleDoc】
/// 責務: Zobrist Hashing による盤面ハッシュ値の生成
/// 入出力: GameContext → ulong (64bit ハッシュ値)
/// 副作用: なし（純粋関数）
/// </summary>
```

### docs 成果物

| ファイル | 内容 |
|---------|------|
| `docs/setup-search-engine.md` | ISearchEngine 導入手順、DI 設定方法 |
| `docs/setup-transposition-table.md` | TT 導入手順、Zobrist 乱数テーブルの説明 |
| `docs/ops-feature-flags.md` | FeatureFlag の使い方（将来の TT ON/OFF 含む） |
| `docs/rollback-task1.md` | Task 1 切り戻し手順 |
| `docs/rollback-task2.md` | Task 2 切り戻し手順 |

### Task 完了時の自己点検項目

| 項目 | 確認内容 |
|-----|---------|
| **変更ファイル一覧** | `git diff --name-only HEAD~1` で確認 |
| **Verify 結果** | `dotnet build` / `dotnet test` の出力を添付 |
| **DoD 充足の根拠** | 各 DoD 項目に対するエビデンス（テスト結果、コードリンク） |
| **残課題** | 実装中に発見した課題・TODO を列挙 |

---

## 5) Risk & Trade-offs

### 主要リスク

| リスク | 影響度 | 発生確率 | 軽減策 |
|-------|-------|---------|--------|
| **TT ハッシュ衝突による誤探索** | 高 | 低 | TTEntry に元 Key を保持し、Probe 時に完全一致検証 |
| **探索結果の回帰（既存と異なる手）** | 高 | 中 | LegacySearchEngine の回帰テストを必須化。既存テストケースを維持 |
| **メモリ使用量増加** | 中 | 中 | `TranspositionTableConfig` でサイズ設定可能。初期値 100万エントリ（約32MB） |
| **DI 設定ミスによる実行時エラー** | 中 | 中 | DI 登録のユニットテスト追加。起動時の依存解決チェック |
| **設計硬直化（将来の MPC 導入困難）** | 高 | 低 | ISearchEngine を十分に抽象化。SearchOptions は最小構成に留める |
| **既存 UI の動作不良** | 中 | 低 | 変更後に WinForms / Console で手動動作確認を必須化 |

### 各リスクの軽減策詳細

1. **TT ハッシュ衝突**
   - TTEntry に `ulong Key` フィールドを持たせる
   - `TryProbe()` で格納された Key と引数 Key の一致を検証
   - 不一致の場合はキャッシュミスとして扱う

2. **探索結果の回帰**
   - Task 1 完了時点では TT は未使用
   - LegacySearchEngine が既存 CachedNegaMax と完全同一の結果を返すことをテスト
   - 既存の `Reluca.Tests/Serchers/` 配下のテストをすべてパスさせる

3. **メモリ使用量**
   - `TranspositionTableConfig` でサイズ指定可能
   - 推測: 将来的に `Resize()` メソッドを追加する可能性あり

4. **DI 設定ミス**
   - `DiProviderUnitTest` を追加し、全サービスの解決テストを実施

### 残存リスク

| 残存リスク | 理由 |
|-----------|------|
| **TT サイズの最適値が未知** | 実際の探索パターンでの検証が必要。後続タスクで調整 |
| **Zobrist 乱数品質** | 固定シードの擬似乱数を使用。理論的には衝突リスクあり。実用上は問題なしと推測 |
| **PVS/ID 未実装のため TT 効果が測定不能** | Task 2 は「箱」のみ作成。実際の効果測定は後続タスク |
