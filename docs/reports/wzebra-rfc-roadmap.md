# WZebra アルゴリズム組み込み RFC ロードマップ

- 作成日: 2026-02-05
- ステータス: 承認済み
- 前提文書: [wzebra-roadmap.md](./wzebra-roadmap.md)（Task 1〜3d.2 完了）、[wzebra-algorithm.md](./wzebra-algorithm.md)

## 1. 概要

WZebra のアルゴリズムを Reluca に組み込む活動として、探索コアの刷新（PVS + 反復深化 + 置換表 + Aspiration Window）が完了した。
本ロードマップは、残りの作業を RFC 単位に分割し、段階的に Reluca の探索エンジンを強化するための計画を定義する。

### 1.1 前提条件

| Task | ステータス | 主な成果 |
|------|-----------|---------|
| Task 1（ISearchEngine 抽象化） | 完了 | 探索エンジン差し替え可能な構造を確立 |
| Task 2（Transposition Table 実装） | 完了 | Zobrist Hash / TTEntry / BoundType の土台構築 |
| Task 3a（PvsSearchEngine） | 完了 | NegaScout ベースの PVS 実装、Legacy と結果一致 |
| Task 3b（TT 統合） | 完了 | Probe / Store / Move Ordering 反映、ON/OFF 一致 |
| Task 3c（反復深化） | 完了 | Iterative Deepening + PV 継承 + TT bestMove |
| Task 3d（Aspiration Window） | 完了 | 狭窓探索 + 再探索ロジック、ON/OFF 一致 |
| Task 3d.1/3d.2（安定性修正） | 完了 | TT Store 抑制方式への移行、キャッシュ破壊バグ修正 |

### 1.2 現状の到達点と課題

探索アルゴリズムとして WZebra の中核構造は完成しているが、以下の課題が残っている。

| # | 課題 | 詳細 | 対応 RFC |
|---|------|------|----------|
| 1 | 性能計測基盤なし | NodesSearched 等の計測手段がなく、改善効果を定量評価できない | RFC `nodes-searched-instrumentation` |
| 2 | 選択的探索未実装 | WZebra の核心技術である Multi-ProbCut が未実装。深さ 14〜16 手が上限 | RFC `multi-probcut` |
| 3 | Aspiration 未最適化 | delta 初期値・拡張戦略がデフォルトのまま。最適なパラメータが不明 | RFC `aspiration-window-tuning` |
| 4 | 時間制御なし | 反復深化と組み合わせた持ち時間制御がなく、実戦での使用に制約 | RFC `time-limit-search` |

## 2. RFC 分割計画

### 2.1 RFC 一覧

| # | slug | タイトル | 主な変更先 | 前提 |
|---|------|---------|-----------|------|
| 1 | `nodes-searched-instrumentation` | 探索ノード数計測の導入 | Reluca/Search | なし |
| 2 | `multi-probcut` | Multi-ProbCut による選択的探索の実装 | Reluca/Search | RFC 1 完了 |
| 3 | `aspiration-window-tuning` | Aspiration Window パラメータ最適化 | Reluca/Search | RFC 1 完了 |
| 4 | `time-limit-search` | 時間制御付き探索の実装 | Reluca/Search | RFC 1 完了 |

### 2.2 RFC 間の依存関係

```
RFC 1 (nodes-searched-instrumentation)
  |
  +-- RFC 2 (multi-probcut) ※最優先
  |     |
  |     +-- [ゲート1: 探索効率評価]
  |
  +-- RFC 3 (aspiration-window-tuning) ※RFC 2 と並行可能
  |
  +-- RFC 4 (time-limit-search) ※RFC 2 と並行可能
  |
  v
[ゲート2: 探索エンジン総合評価]
  |
  v
将来拡張（並列探索、ZobristHash 差分更新、TT 最適化）
```

## 3. 各 RFC の詳細

### 3.1 RFC 1: 探索ノード数計測の導入

**slug**: `nodes-searched-instrumentation`

**スコープ**:
- `SearchResult` に `NodesSearched` プロパティを追加
- PVS 探索の各ノード展開時にカウントを加算
- TT Probe ヒット時のカウント処理
- 反復深化の各深さごとの NodesSearched をログ出力可能にする

**主な変更先**: `Reluca/Search/SearchResult.cs`, `Reluca/Search/PvsSearchEngine.cs`

**完了基準**:
- 探索完了時に NodesSearched が正の値として返されること
- TT ON/OFF で NodesSearched の差分が確認できること（TT ON 時に減少）
- 既存テストが全て通過すること（探索結果に影響しない）

**優先度**: **最優先**（他の全ての RFC の前提）

---

### 3.2 RFC 2: Multi-ProbCut による選択的探索の実装

**slug**: `multi-probcut`

**スコープ**:
- ProbCut の回帰モデル（$v_d \approx a \cdot v_{d'} + b + e$）の実装
- カットペアの定義（浅い探索深さ → 深い探索深さの組み合わせ）
- ゲームステージ（石数ベース）に応じた回帰パラメータ（$a, b, \sigma$）の管理
- 信頼度（$p$）に基づく枝刈り判定式の実装
- MPC ON/OFF 切り替え機能（`SearchOptions` への追加）

**主な変更先**: `Reluca/Search/PvsSearchEngine.cs`, `Reluca/Search/SearchOptions.cs`

**設計上の検討事項**:
- **回帰パラメータの決定方法**: 自己対戦データからの学習 or WZebra 文献ベースの初期値設定 → RFC 内で決定
- **カットペアの構成**: (d'=2, d=6), (d'=4, d=10), (d'=6, d=14) 等の多段階構成を検討
- **信頼度の初期値**: 95%（$p = 0.95$）を基準とし、ステージごとに調整

**完了基準**:
- MPC ON 時に NodesSearched が MPC OFF 時と比較して有意に減少すること
- MPC ON 時の探索深さが MPC OFF 時より深くなること（同一時間内）
- MPC ON/OFF いずれでも致命的な着手品質の劣化がないこと（対 Legacy 勝率で検証）

**優先度**: **高**（WZebra の核心技術。探索深度の飛躍に直結）

---

### 3.3 RFC 3: Aspiration Window パラメータ最適化

**slug**: `aspiration-window-tuning`

**スコープ**:
- delta 初期値の最適化（現在のデフォルト 50 からの調整）
- 再探索時の拡張戦略の改善（固定幅 → 指数拡張等）
- 最大リトライ回数（現在 3）の妥当性検証
- ステージ別パラメータの検討

**主な変更先**: `Reluca/Search/SearchOptions.cs`, `Reluca/Search/PvsSearchEngine.cs`

**完了基準**:
- チューニング後の NodesSearched がチューニング前と比較して改善すること
- retry 発生率と探索効率のトレードオフが定量的に整理されていること
- 既存テストが全て通過すること

**優先度**: **中**（RFC 2 と並行可能。MPC との相乗効果が期待される）

---

### 3.4 RFC 4: 時間制御付き探索の実装

**slug**: `time-limit-search`

**スコープ**:
- `SearchOptions` に `TimeLimitMs` パラメータを追加
- 反復深化ループで経過時間を監視し、制限時間超過時に直前の深さの結果を返す
- 探索中のノード展開時にタイムアウトチェックを実施（チェック頻度の設計）
- 持ち時間配分戦略の実装（残り手数に応じた動的な持ち時間配分）

**主な変更先**: `Reluca/Search/SearchOptions.cs`, `Reluca/Search/PvsSearchEngine.cs`

**完了基準**:
- 指定した制限時間内に探索が完了し、有効な着手が返されること
- 制限時間が短い場合でも最低 1 手分の探索結果が保証されること
- TimeLimitMs 未指定時は従来通り MaxDepth まで探索すること

**優先度**: **中**（実戦利用の前提。RFC 2 と並行可能）

---

## 4. ゲート（Phase 間の評価ポイント）

### ゲート1: 探索効率評価（RFC 2 完了後）

**評価項目**:
- MPC ON 時の NodesSearched が MPC OFF 時と比較してどの程度削減されたか
- 同一時間内での到達探索深度の改善幅
- 対 Legacy 勝率に劣化がないか

**判断基準**:
- **通過**: NodesSearched が 50% 以上削減、かつ対 Legacy 勝率が同等以上 → RFC 3, 4 に進む
- **条件付き通過**: NodesSearched は削減されたが勝率に軽微な劣化 → パラメータ調整後に再評価
- **不通過**: NodesSearched の削減が不十分 or 勝率が大幅に劣化 → 回帰パラメータ・カットペアの見直し

---

### ゲート2: 探索エンジン総合評価（RFC 2〜4 完了後）

**評価項目**:
- Legacy 比での到達探索深度（同一時間内）
- Legacy 比での対局勝率
- 時間制御下での着手品質の安定性

**判断基準**:
- **完遂**: Legacy を明確に上回る探索効率と勝率 → 探索コアとして正式採用
- **部分完遂**: 一部指標で改善が見られるが Legacy と同等 → 将来拡張で対応
- **不達成**: Legacy に劣る → 根本的なアーキテクチャ再検討

---

## 5. 将来拡張（RFC 外）

RFC 1〜4 の完遂後、必要に応じて以下を検討する。

| 項目 | 概要 | 着手条件 |
|------|------|----------|
| 並列探索 | Root Split / YBWC 等によるマルチスレッド探索 | ゲート2 通過後、探索速度がボトルネックとなった場合 |
| ZobristHash 差分更新 | MakeMove/UnmakeMove 時のハッシュ差分計算 | TT のヒット率改善が必要な場合 |
| TT サイズ・置換戦略の最適化 | エントリ数、置換ポリシー（Depth-Preferred 等）の調整 | NodesSearched 計測データに基づいて判断 |
| パターン評価関数の機械学習 | Logistello 方式のパターン重み自動学習 | 探索コアが安定した後の次フェーズ |

---

## 6. 進行ルール

### 6.1 RFC の格納先

全ての RFC 文書は Reluca リポジトリの `docs/rfcs/<YYYYMMDD>-<slug>/` に格納する。

### 6.2 順序の厳守

RFC 1 を最初に完了させること。RFC 2〜4 は RFC 1 完了後に着手し、並行実施可能。
ただし RFC 2（Multi-ProbCut）を最優先で進めることを推奨する。

### 6.3 ゲート評価の記録

各ゲート評価の結果は `docs/reports/` 配下に記録する。

### 6.4 検証方針

各 RFC の実装は以下の原則で検証する。

| ルール | 内容 |
|--------|------|
| 正しさの保証 | 機能 ON/OFF 切り替えで探索結果が一貫していることを確認 |
| 性能の定量評価 | NodesSearched を主指標として改善効果を数値で示す |
| 回帰テスト | 既存の全テストが通過すること |

---

## 7. 現在の状態

| RFC | ステータス | ゲート | 備考 |
|-----|----------|--------|------|
| RFC 1: `nodes-searched-instrumentation` | 未着手 | - | **次に実施** |
| RFC 2: `multi-probcut` | 未着手 | ゲート1 | RFC 1 完了後、最優先 |
| RFC 3: `aspiration-window-tuning` | 未着手 | - | RFC 1 完了後（RFC 2 と並行可） |
| RFC 4: `time-limit-search` | 未着手 | - | RFC 1 完了後（RFC 2 と並行可） |
| 探索エンジン総合評価 | - | ゲート2 | RFC 2〜4 完了後 |
