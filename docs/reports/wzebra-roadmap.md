# WZebra 組み込みロードマップ（現状整理と完遂まで）

## 背景・前提

本ロードマップは、Reluca をさらに強化するための取り組みの一環として策定したものである。
具体的には、[WZebra のアルゴリズム解析レポート](./wzebra-algorithm.md)に記載されている WZebra の各種アルゴリズム（Multi-ProbCut による選択的探索、パターンベース評価関数、NegaScout、反復深化、置換表など）を Reluca に組み込む活動を進めており、本ロードマップはその対応状況と今後の計画を整理したものである。

---

## 1. これまでに何をしてきたか

### 基盤整備フェーズ

- **Task 1**
  - ISearchEngine 抽象化
  - LegacySearchEngine 導入
  - 探索エンジン差し替え可能な構造を確立
  - 正しさの基準を「Legacy と一致」に固定

- **Task 2**
  - Transposition Table（TT）の箱を実装
  - Zobrist Hash / TTEntry / BoundType / Config
  - 探索には未使用（正しさ影響ゼロの土台）

### 探索コア刷新フェーズ

- **Task 3a**
  - Template Method から完全離脱
  - 明示的制御フローの PvsSearchEngine 実装
  - Legacy と探索結果一致を保証

- **Task 3b**
  - TT を PVS に統合
  - Probe / Store / Move Ordering 反映
  - TT ON/OFF で結果一致を担保

- **Task 3c**
  - 反復深化（Iterative Deepening）導入
  - RootSearch 分離
  - PV 継承 + TT bestMove による手順序改善

- **Task 3d**
  - Aspiration Window 導入
  - 狭窓探索 + 再探索ロジック実装
  - ON/OFF 一致テストで正しさ保証

- **Task 3d.1 / 3d.2（重要な実地修正）**
  - Aspiration retry 中の TT Store 問題を解析
  - TT Clear 乱用を排除
  - TT Store 抑制フラグ方式に移行
  - 正しさと性能改善の両立に成功

### 安定性修正

- **キャッシュ破壊バグ修正（VS 並列テスト問題）**
  - Cache null 化を完全排除
  - Dispose の責務を Clear のみに限定
  - テスト安定性を環境非依存に修正

---

### 現状の到達点

- PVS + ID + TT + Aspiration が正しく統合済み
- Legacy / TT ON-OFF / Aspiration ON-OFF すべて一致
- 探索アルゴリズムとして WZebra 中核構造は完成
- ただし「性能を語るための数値」がまだ無い

---

## 2. この先何をすれば完遂か

### 次タスク（強く推奨・必須）

- **Task 3e: NodesSearched 計測**
  - SearchResult に NodesSearched を追加
  - Pvs / RootSearch / Probe ヒット等で加算
  - 正しさに一切影響しない
  - 以降の全性能議論の前提インフラ

### 性能最適化フェーズ（NodesSearched 前提）

- **Multi-ProbCut（MPC）**
  - 浅探索 + 統計的枝刈り
  - NodesSearched 減少を数値で検証可能

- **Aspiration Window チューニング**
  - delta 初期値・拡張戦略の最適化
  - retry 回数と探索効率のトレードオフ調整

- **TimeLimitMs 対応**
  - 反復深化と組み合わせた時間制御
  - 実戦用探索エンジンとして完成

### 拡張（任意・将来）

- 並列探索（root split / YBWC 等）
- ZobristHash 差分更新（UpdateHash）
- TT サイズ・置換戦略の最適化

---

### 完遂の定義

- NodesSearched を根拠に性能改善を説明できる
- Legacy を完全に超えた探索効率を数値で示せる
- 探索コアが「理論・実装・検証」すべて揃った状態

---

## 結論

現在地点は「WZebra を組み込むための探索アルゴリズム実装」は事実上完了している。

残っているのは「性能を測り、語り、最適化するための計測と仕上げ」。

次は Task 3e（NodesSearched）から再開するのが最短・最善ルート。
