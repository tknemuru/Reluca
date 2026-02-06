## Technical Quality Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Request Changes

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **データクラスの不変設計 (Section 5.3)**: `MpcParameters` と `MpcCutPair` がイミュータブルに設計されており、探索中の状態汚染リスクが排除されている。
- **DI 統合 (Section 5.6)**: `MpcParameterTable` を Singleton として DI に登録し、コンストラクタインジェクションで `PvsSearchEngine` に渡す設計は、既存の DI パターンとの一貫性が高い。
- **可観測性 (Section 7.2)**: 反復深化の各深さ完了時のログに `MpcCuts` を追加する設計は、既存のログ構造を自然に拡張しており、デバッグ・チューニング時の情報取得に有効である。
- **TT Probe 後の MPC 配置 (Section 5.4.1)**: TT ヒット時に MPC 判定をスキップする配置は、無駄な浅い探索を回避する合理的な設計である。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**[P0-1] TryMultiProbCut の浅い探索で Aspiration Window 状態が考慮されていない**
- **対象セクション**: 5.4.2 TryMultiProbCut メソッド
- **内容**: `TryMultiProbCut` 内の浅い探索は `Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false)` を呼び出すが、この探索が Aspiration Window の retry ループ中に実行された場合、`_suppressTTStore = true` の状態で浅い探索が TT Store を抑制したまま実行される。MPC 用の浅い探索は独立した探索であり、Aspiration retry の TT Store 抑制の影響を受けるべきではない。
- **修正の期待値**: `TryMultiProbCut` 内で浅い探索を実行する際に、`_mpcEnabled` と同様に `_suppressTTStore` も一時的に `false` に退避・復元する処理を追加すること。

**[P1-1] MpcParameterTable の BuildDefaultTable が省略記号で実装が不明**
- **対象セクション**: 5.3.3 MpcParameterTable クラス
- **内容**: `BuildDefaultTable()` の実装が `// ... ステージごとのパラメータ設定 ...` と省略されており、15 ステージ x 3 カットペア = 45 エントリのパラメータ登録の具体的な実装パターンが不明である。Section 5.3.4 に具体値の表はあるが、コード上の登録方法（ループで生成するのか、ステージ区分ごとにまとめるのか）が示されていない。
- **修正の期待値**: `BuildDefaultTable()` の実装を、ステージ区分（序盤/中盤/終盤）ごとのループパターンとして具体的に記述すること。

**[P1-2] _mpcCutCount カウンタの宣言・リセット位置が未定義**
- **対象セクション**: 5.4.2 TryMultiProbCut メソッド / 7.2 可観測性
- **内容**: Section 7.2 で `_mpcCutCount` がログ出力に含まれているが、このフィールドの宣言位置、初期化タイミング（`Search()` の冒頭か、各反復の冒頭か）、インクリメント位置（`TryMultiProbCut` のカット成立時）が設計に記載されていない。
- **修正の期待値**: `_mpcCutCount` のフィールド宣言を Section 5.4 に追加し、リセットタイミング（各反復深化の開始時に `_nodesSearched` と同様にリセット）とインクリメント位置（`TryMultiProbCut` でカットが成立し `return` する直前）を明記すること。

**[P2-1] MpcParameterTable を record 型または readonly struct で表現する選択肢**
- **対象セクション**: 5.3.1 MpcParameters クラス / 5.3.2 MpcCutPair クラス
- **内容**: `MpcParameters` と `MpcCutPair` はイミュータブルなデータクラスであり、C# 12 の `record` 型（`public record MpcParameters(double A, double B, double Sigma)`）で表現すると、ボイラープレートコードの削減と構造的等値比較のサポートが得られる。既存コードベースで `record` がどの程度使用されているかに依存するため、記録のみとする。
