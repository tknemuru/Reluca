# [RFC] システム概要ドキュメントの作成

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | Claude |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-04 |
| **タグ** | documentation, architecture |
| **関連リンク** | - |

## 1. 要約 (Summary)

- 本 RFC は、Reluca リポジトリにシステム概要ドキュメント（`docs/architecture.md`, `docs/domain-model.md`）を新規作成することを提案する。
- IEEE 1016 Software Design Description の Structure / Data ビューポイントに対応し、システムの全体像を簡潔に記述する。
- API エンドポイントを持たないライブラリであるため、`docs/api-overview.md` は作成対象外とする。

## 2. 背景・動機 (Motivation)

- 現状、Reluca リポジトリにはシステム全体を俯瞰するドキュメントが存在しない。
- コードベースは 65 以上の C# ファイルを持ち、探索エンジン・評価関数・キャッシング・置換表など複数のサブシステムで構成されている。
- 新規参画者やレビュアーがアーキテクチャやドメインモデルを把握するには、ソースコードを直接読み解く必要がある。
- `/rfc`, `/imp` などのワークフローコマンドがシステム概要ドキュメントを参照する設計になっており、ドキュメントがないとワークフローが不完全になる。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- `docs/architecture.md` を作成し、ディレクトリ構成・レイヤー構造・依存関係・技術選定を記述する。
- `docs/domain-model.md` を作成し、ドメイン概念・データモデル・状態遷移を記述する。
- ドキュメントは日本語で記述し、開発者が迷わず理解できる粒度とする。

### やらないこと (Non-Goals)

- `docs/api-overview.md` の作成（本プロジェクトは Web API を提供しないため）。
- 詳細な API リファレンスやクラスごとの Javadoc 的ドキュメントの作成。
- ドキュメント自動生成ツールの導入。

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- 本 RFC の実装に外部ライブラリや環境変更は不要である。
- Markdown 形式で `docs/` ディレクトリ配下に配置する。

## 5. 詳細設計 (Detailed Design)

### 5.1 docs/architecture.md の構成

以下の構成で記述する。

```markdown
# Architecture

## 概要
（システムの目的・主要機能の概要）

## ディレクトリ構成
Reluca/
├── Reluca/           # メインライブラリ
├── Reluca.Tests/     # 単体テスト
├── Reluca.Ui.WinForms/  # UI 層
├── Reluca.Tools/     # ツール
└── docs/             # ドキュメント

## レイヤー構造
（UI 層 → ビジネスロジック層 → データ層 の依存関係図）

## 主要コンポーネント
- Search: 探索エンジン（PVS/NegaScout, 反復深化, Aspiration Window）
- Evaluates: 局面評価（特徴パターン評価）
- Movers: 指し手決定
- Updaters: ゲーム状態更新
- Cachers: キャッシング
- Transposition: 置換表（Zobrist ハッシング）

## 技術スタック
- C# 12 / .NET 8.0
- Microsoft.Extensions.DependencyInjection
- xUnit（テスト）
- Windows Forms（UI）
```

### 5.2 docs/domain-model.md の構成

以下の構成で記述する。

```markdown
# Domain Model

## 概要
（オセロ/リバーシのドメインモデル概要）

## ドメイン概念
- Disc: 石（Black, White）
- Board: 盤面（8×8, ビット表現）
- Player: プレイヤー（Human, Cpu）
- Stage: ゲーム進行ステージ

## データ構造
### 盤面表現
- ulong (64 ビット) で 8×8 盤を表現
- Black: 黒石の配置
- White: 白石の配置
- Mobility: 着手可能位置

### コンテキスト
- GameContext: ゲーム全体の状態（record 型）
- BoardContext: 盤面状態（record 型）

## 特徴パターン
（13 種類の評価用パターンの説明）

## 状態遷移
（ゲームフロー: 初期化 → 着手 → 裏返し → 終了判定）
```

### 5.3 作成対象ファイル

| ファイルパス | 目的 |
|-------------|------|
| `docs/architecture.md` | システム構造の把握 |
| `docs/domain-model.md` | ドメイン概念・データの把握 |

## 6. 代替案の検討 (Alternatives Considered)

### 案A: 3 ドキュメントすべてを作成

- **概要**: CLAUDE.md で定義された 3 ドキュメント（architecture.md, domain-model.md, api-overview.md）をすべて作成する。
- **長所**: 規約との整合性が高い。
- **短所**: api-overview.md は本プロジェクトに該当しないため、中身のないドキュメントになる。

### 案B: 2 ドキュメントのみ作成（採用案）

- **概要**: architecture.md と domain-model.md のみを作成し、api-overview.md は作成しない。
- **長所**: 実態に即したドキュメント構成となる。不要なドキュメントを作らない。
- **短所**: 規約の「省略可」ルールに依存する。

### 選定理由

- CLAUDE.md には「既存リポジトリやリポジトリの特性上不要な場合は省略可」と明記されている。
- Reluca は Web API を提供しないライブラリであり、api-overview.md を作成しても意味のある内容を記載できない。
- 案B を採用する。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

- 該当なし（ドキュメント作成のみ）。

### 7.2 スケーラビリティとパフォーマンス

- 該当なし。

### 7.3 可観測性 (Observability)

- 該当なし。

### 7.4 マイグレーションと後方互換性

- 新規ファイル追加のみであり、既存コードへの影響はない。

## 8. テスト戦略 (Test Strategy)

- ドキュメント作成のためテストは不要である。
- Markdown 構文の妥当性は目視レビューで確認する。

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ 1: ドキュメント作成

1. `docs/architecture.md` を作成
2. `docs/domain-model.md` を作成

### フェーズ 2: レビュー・マージ

1. PR レビュー
2. main ブランチへマージ

### 検証方法

- レビュアーによる内容の正確性確認。
- ワークフローコマンド（`/rfc`, `/imp`）でドキュメントが正しく参照されることを確認。

### システム概要ドキュメントへの影響

- 本 RFC により `docs/architecture.md` と `docs/domain-model.md` が新規作成される。
- 既存のシステム概要ドキュメントは存在しないため、更新対象はない。
