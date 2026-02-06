# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 1 件（アクション対象: 1 件）
- **P2 件数**: 6 件（記録のみ）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) — 上位5件

**[P1-1] MpcParameterTable のカットペア数・ステージ数のマジックナンバー排除**
- **出典**: Technical Quality Reviewer
- **内容**: `BuildDefaultTable()` 内のループ上限 `stage <= 15` と `cutPairIndex < 3` がマジックナンバーとして記述されている。`Stage.Max` と `CutPairs.Count` を参照する形に変更することで、カットペアの追加やステージ数の変更時にコード修正箇所が削減される。
- **修正方針**: `for (int stage = 1; stage <= 15; stage++)` を `for (int stage = 1; stage <= Stage.Max; stage++)` に、`cutPairIndex < 3` を `cutPairIndex < CutPairs.Count` に変更する。`CutPairs` が `BuildDefaultTable()` 呼出前に初期化済みであることを確認すること。

## 記録のみ (P2)

- MPC 浅い探索の NodesSearched カウントに関する明確化: MPC 用の浅い探索ノード数が `NodesSearched` に含まれる点について、将来のパラメータ調整フェーズで考慮する（出典: Approach Reviewer）
- テスト `MPC_ON_OFFでBestMoveとValueが一致する` の検証方法: テスト名と実際の検証内容（合法手範囲の検証）の乖離は、コメントで正しく注記されており妥当である（出典: Approach Reviewer）
- シングルスレッド前提の save/restore パターンのリスク記録: 将来の並列探索導入時にフラグ管理方式の変更が必須となる（出典: Security & Risk Reviewer）
- DefaultAlpha/DefaultBeta を用いた浅い探索のオーバーフローリスク: `double` キャスト時の精度損失は理論上存在するが、実際の探索フローでは TT Probe やウィンドウ縮小が先行するため実害は低い（出典: Security & Risk Reviewer）
- テストメソッド名 `MPC_ON_OFFでBestMoveとValueが一致する` のリネーム提案: 実際の検証内容に合わせた名前にすると意図が明確になる（出典: Technical Quality Reviewer）
- MpcParameterTable の BuildDefaultTable における文字列キーの使用: enum や定数を使う方がタイプセーフだが、初期化処理であり対応不要（出典: Technical Quality Reviewer）

## 要判断（矛盾する指摘）

該当なし
