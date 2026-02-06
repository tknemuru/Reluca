# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 1 件
- **P1 件数**: 6 件（アクション対象: 5 件）
- **P2 件数**: 4 件（記録のみ）

## 必須対応 (P0)

**[P0-1] TryMultiProbCut の浅い探索で Aspiration Window の TT Store 抑制状態が考慮されていない**
- **出典**: Quality
- **内容**: `TryMultiProbCut` 内の浅い探索が Aspiration Window の retry ループ中に実行された場合、`_suppressTTStore = true` の状態が浅い探索にも波及する。MPC 用の浅い探索は独立した探索であり、Aspiration retry の TT Store 抑制の影響を受けるべきではない。
- **修正方針**: `TryMultiProbCut` 内で浅い探索を実行する際に、`_mpcEnabled` の退避・復元と同様に `_suppressTTStore` も一時的に `false` に退避・復元する処理を追加する。具体的には以下のコードパターンとする。

```csharp
bool savedMpcFlag = _mpcEnabled;
bool savedSuppressTTStore = _suppressTTStore;
_mpcEnabled = false;
_suppressTTStore = false;
long shallowValue = Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false);
_mpcEnabled = savedMpcFlag;
_suppressTTStore = savedSuppressTTStore;
```

## 推奨対応 (P1) — 上位5件

**[P1-1] 初期パラメータの σ 値に対する検証基準の定義**
- **出典**: Approach
- **内容**: σ 値が「仮設定」と明記されているが、調整の判定基準（どの指標がどの閾値を下回ったら σ を変更するか）が定義されていない。
- **修正方針**: Section 8（テスト戦略）に「σ 調整の判定基準」を追加する。NodesSearched 削減率と対 Legacy 勝率の 2 軸で具体的な数値閾値を定め、σ の増減方針を記載すること。

**[P1-2] _mpcCutCount カウンタの設計詳細の明記**
- **出典**: Quality
- **内容**: `_mpcCutCount` のフィールド宣言位置、初期化タイミング、インクリメント位置が設計に記載されていない。
- **修正方針**: Section 5.4 に `_mpcCutCount` のフィールド宣言を追加し、リセットタイミング（各反復深化の開始時に `_nodesSearched` と同様にリセット）とインクリメント位置（`TryMultiProbCut` でカットが成立し `return` する直前）を明記する。

**[P1-3] _mpcEnabled フラグのシングルスレッド前提の明記**
- **出典**: Security
- **内容**: `_mpcEnabled` のフラグ save/restore がシングルスレッド前提の設計であることが明示されていない。Non-Goals に「並列探索との統合」はあるが、このフラグ設計がその前提に依存していることの注記がない。
- **修正方針**: Section 5.4.3 に「本設計はシングルスレッド前提である。並列探索の導入時には `_mpcEnabled` および `_suppressTTStore` の管理方式をスレッドローカルまたは引数渡しに変更する必要がある」旨の注記を追加する。

**[P1-4] BuildDefaultTable の具体的な実装パターンの記述**
- **出典**: Quality
- **内容**: `BuildDefaultTable()` の実装が省略記号で示されており、45 エントリのパラメータ登録の具体的な実装パターンが不明である。
- **修正方針**: `BuildDefaultTable()` の実装を、ステージ区分（序盤 1-5 / 中盤 6-10 / 終盤 11-15）ごとのループパターンとして具体的に記述する。

**[P1-5] カットペア構成の選定根拠の文献引用**
- **出典**: Approach
- **内容**: カットペアの深さが「WZebra の文献に基づく典型的な構成」とされているが、具体的な文献引用がない。
- **修正方針**: WZebra/Logistello の論文・ソースコードから具体的なカットペア構成を引用し、本 RFC のペア選定との対応関係を記載する。

※ P1 は 6 件報告されたが、上位 5 件をアクション対象とした。以下 1 件を省略した。
- 省略: フルウィンドウ浅い探索の最悪ケースにおけるノード数の定量的見積もり追記（Security, P1-2）

## 記録のみ (P2)

- MPC の効果が目標に到達しなかった場合のフォールバック戦略の明示。MPC 無効化リリースか別手法検討かの判断基準を記載しておくとよい（出典: Approach）
- ステージ境界（例: ステージ 5→6）における σ 値の不連続変化が探索安定性に影響する可能性。将来のパラメータ最適化時に線形補間等の平滑化を検討する価値がある（出典: Approach）
- 浮動小数点演算の丸め誤差が境界値付近でカット判定を変える可能性。`Math.Ceiling`/`Math.Floor` の使用により一貫性が保たれるが、デバッグ時に留意すべき事項である（出典: Security）
- `MpcParameters` / `MpcCutPair` を C# 12 の `record` 型で表現するとボイラープレート削減と構造的等値比較のサポートが得られる。既存コードベースの `record` 使用状況に依存する（出典: Quality）

## 要判断（矛盾する指摘）

該当なし
