# ゲート2: 探索エンジン総合評価 — 実施手順書

- 作成日: 2026-02-09
- 前提文書: [wzebra-rfc-roadmap.md](./wzebra-rfc-roadmap.md)
- ステータス: ドラフト

## 1. 目的

RFC 1〜4（探索ノード数計測、Multi-ProbCut、Aspiration Window チューニング、時間制御付き探索）の実装完了を受けて、PvsSearchEngine の総合評価を実施する。

評価結果に基づき、PvsSearchEngine を Reluca の探索コアとして正式採用するかどうかを判断する。

## 2. 評価項目（ロードマップより）

| # | 評価項目 | 概要 |
|---|---------|------|
| E1 | Legacy 比での到達探索深度（同一時間内） | 同じ制限時間で PVS がどこまで深く読めるか |
| E2 | Legacy 比での対局勝率 | PVS vs Legacy の対戦結果 |
| E3 | 時間制御下での着手品質の安定性 | 時間制限ありでも一貫した手を返すか |

## 3. 前提条件

### 3.1 現状の実装状態

| 項目 | 状態 |
|------|------|
| PvsSearchEngine | 完全実装済み（PVS + TT + 反復深化 + Aspiration + MPC + 時間制御） |
| LegacySearchEngine | **削除済み**（比較対象として存在しない） |
| 対局システム | **未実装**（2エンジン対戦の仕組みがない） |
| ベンチマークプログラム | **未実装** |

### 3.2 重要な制約: LegacySearchEngine が存在しない

LegacySearchEngine は既に削除されているため、「Legacy 比」の評価はそのままでは実施不能である。
以下の代替方針で評価を進める。

**代替方針: 機能 ON/OFF による段階比較**

PvsSearchEngine は SearchOptions で各機能を個別に ON/OFF できるため、以下の構成を比較対象とする。

| 構成名 | TT | Aspiration | MPC | 時間制御 | 位置付け |
|--------|:--:|:----------:|:---:|:--------:|---------|
| Baseline | OFF | OFF | OFF | なし | Legacy 相当（素の NegaScout） |
| TT-Only | ON | OFF | OFF | なし | TT のみ有効 |
| Full | ON | ON | ON | なし | 全機能有効（時間制限なし） |
| Full+Time | ON | ON | ON | あり | 全機能有効（時間制限あり） |

## 4. 評価手順

### Step 1: ゲート1評価の実施（事前確認）

ゲート1（MPC 探索効率評価）の結果が未記録のため、ゲート2の一環として先に実施する。

#### 1-1. MPC ON/OFF の NodesSearched 比較

**手順:**
1. テスト局面（初期局面 + 中盤局面 2〜3 面）を用意する
2. 各局面に対し、探索深さ 7, 9, 11 で以下を実行する
   - MPC OFF（TT ON）: `SearchOptions(depth, useTranspositionTable: true, useMultiProbCut: false)`
   - MPC ON（TT ON）: `SearchOptions(depth, useTranspositionTable: true, useMultiProbCut: true)`
3. 各条件で NodesSearched, BestMove, Value, ElapsedMs を記録する
4. NodesSearched の削減率を算出する: `(OFF - ON) / OFF * 100`

**判断基準:**
- 通過: NodesSearched が 50% 以上削減
- 条件付き通過: 削減はあるが 50% 未満
- 不通過: 削減がない or 悪化

**実施方法:** コンソールアプリケーション（`Reluca.Tools` に評価用コマンドを追加）で自動実行し、結果をコンソール出力する。

---

### Step 2: E1 — 到達探索深度の比較

**目的:** 同一制限時間で Baseline 構成と Full 構成がどの深さまで到達できるかを比較する。

**手順:**
1. テスト局面を 3〜5 面用意する（序盤・中盤をカバー）
2. 各局面に対し、制限時間 1000ms, 3000ms, 5000ms で以下を実行する
   - Baseline: `SearchOptions(maxDepth: 20, useTranspositionTable: false, timeLimitMs: T)`
   - Full+Time: `SearchOptions(maxDepth: 20, useTranspositionTable: true, useAspirationWindow: true, aspirationUseStageTable: true, useMultiProbCut: true, timeLimitMs: T)`
3. 各条件で CompletedDepth, NodesSearched, ElapsedMs を記録する
4. Full+Time と Baseline の CompletedDepth の差を算出する

**期待値:** Full+Time が Baseline を 2〜4 手以上上回ること。

---

### Step 3: E2 — 対局勝率の比較

**目的:** Baseline 構成と Full 構成を対戦させ、Full 構成が同等以上の勝率を持つことを確認する。

**手順:**
1. 対局システムの簡易実装（後述の Step 0 で実装する）
2. 以下の対戦カードを実施する
   - Full（深さ 7）vs Baseline（深さ 7）: 各 10 対局（先手後手交替）
   - Full+Time（1000ms）vs Baseline（深さ 7）: 各 10 対局（先手後手交替）
3. 各対戦カードで勝率・引き分け率を記録する
4. 石差の平均も記録する

**判断基準:**
- 完遂: Full 構成の勝率が 60% 以上
- 部分完遂: 勝率 50% 以上 60% 未満
- 不達成: 勝率 50% 未満

---

### Step 4: E3 — 時間制御下での着手品質の安定性

**目的:** 時間制限ありでも安定した着手品質が保たれることを確認する。

**手順:**
1. テスト局面を 5 面用意する
2. 各局面に対し、以下の条件で探索を実行する
   - 時間制限なし（Full, depth=7）: BestMove を「正解手」とする
   - 時間制限あり（Full+Time, 500ms / 1000ms / 3000ms）: BestMove を記録する
3. 以下の指標を評価する
   - **一致率**: 時間制限ありの BestMove が時間制限なしと一致した割合
   - **CompletedDepth**: 各制限時間での到達深さの分布
   - **制限時間遵守率**: ElapsedMs が TimeLimitMs を大幅に超過していないか

**判断基準:**
- 安定: 一致率 80% 以上、制限時間遵守率 100%
- 許容: 一致率 60% 以上、制限時間遵守率 90% 以上
- 不安定: 上記を満たさない

---

## 5. 実装が必要なインフラ

### 5.1 評価用コンソールコマンド（Reluca.Tools に追加）

評価を手動テストではなく再現可能な形で実施するため、`Reluca.Tools` にベンチマーク用コマンドを追加する。

```
dotnet run --project Reluca.Tools -- benchmark [サブコマンド]
```

#### サブコマンド一覧

| サブコマンド | 説明 | 出力 |
|-------------|------|------|
| `mpc-comparison` | MPC ON/OFF の NodesSearched 比較 | 局面ごとの NodesSearched, 削減率 |
| `depth-comparison` | Baseline vs Full の到達深度比較 | 局面 × 制限時間ごとの CompletedDepth |
| `time-stability` | 時間制御下での着手安定性検証 | 一致率, CompletedDepth 分布, 制限時間遵守率 |

### 5.2 簡易対局システム

Step 3（対局勝率）のために必要な最小限の対局機能。

**必要なコンポーネント:**
- `SimpleMatch`: 2つの SearchOptions を受け取り、1対局を最後まで進行する
  - 合法手生成 → 探索 → 着手 → 盤面更新 → 繰り返し
  - パス・終局判定
  - 石数カウントによる勝敗判定
- `MatchRunner`: 指定回数の対局を実行し、勝率・石差平均を集計する

**実行方法:**
```
dotnet run --project Reluca.Tools -- benchmark match --games 10
```

## 6. テスト局面

以下の局面をテスト局面として使用する。既存のテストリソースから流用可能なものを使い、不足分は追加する。

| # | 局面 | ターン数 | 特徴 | ソース |
|---|------|---------|------|--------|
| 1 | 初期局面 | 4 | 対称性あり。序盤の分岐が少ない | テストリソース index=1 |
| 2 | 中盤局面 A | 20前後 | 分岐が多い典型的な中盤 | テストリソース index=2 |
| 3 | 中盤局面 B | 30前後 | 石数が増え評価が安定してくる中盤後半 | 新規追加 |
| 4 | 終盤手前 | 40前後 | 読み切りが視野に入る局面 | 新規追加 |

※ テスト局面は `Reluca.Tests/Resources/PvsSearchEngine/` に既存の JSON 形式で格納されている。

## 7. 実施計画

| # | タスク | 依存 | 見積規模 |
|---|-------|------|---------|
| 0 | テスト局面の準備（既存確認 + 不足分追加） | なし | 小 |
| 1 | 評価用ベンチマークコマンドの実装（mpc-comparison, depth-comparison, time-stability） | 0 | 中 |
| 2 | 簡易対局システムの実装（SimpleMatch + MatchRunner） | 0 | 中 |
| 3 | Step 1 実行（MPC 比較 = ゲート1） | 1 | 小 |
| 4 | Step 2 実行（到達深度比較） | 1 | 小 |
| 5 | Step 3 実行（対局勝率） | 2 | 小 |
| 6 | Step 4 実行（着手安定性） | 1 | 小 |
| 7 | 結果集約・判定・レポート作成 | 3, 4, 5, 6 | 小 |

### 7.1 RFC 化の判断

タスク 1〜2（ベンチマークコマンド + 対局システム）は評価インフラの実装であり、RFC を起こすかどうかは規模次第で判断する。ゲート2の評価作業自体は RFC 不要で、結果を `docs/reports/` に記録する。

## 8. 判定基準（総合）

| 判定 | 条件 |
|------|------|
| **完遂** | E1: Full が Baseline を 2手以上上回る AND E2: 勝率 60% 以上 AND E3: 一致率 80% 以上 |
| **部分完遂** | 上記のうち 2 項目以上を満たすが全ては満たさない |
| **不達成** | E2 の勝率が 50% 未満、または E3 の制限時間遵守率が 90% 未満 |

## 9. 出力物

- `docs/reports/YYYYMMDD-report-gate2-evaluation-results.md`: ゲート2 評価結果レポート
  - ゲート1（MPC 比較）の結果も含む
  - 各評価項目の測定値と判定結果
  - 総合判定と次のアクション
