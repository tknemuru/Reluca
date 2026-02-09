# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 3 件（アクション対象: 3 件）
- **P2 件数**: 4 件（記録のみ）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) -- 上位5件

**[P1-1] タイムアウト復元時の _patternChangeOffset 安全性に関するコメント追記**
- **出典**: Security & Risk Reviewer
- **内容**: `MakeMove` 内の `UpdatePatternIndicesForSquare` ループ中にタイムアウト例外が発生した場合、`_patternChangeOffset` が中途半端に進む可能性がある。現行実装では `ThrowIfTimeout` は `Pvs` 冒頭でのみ呼ばれるため実害はないが、将来の改修者が `MakeMove` 内にタイムアウトチェックを追加するリスクがある。
- **修正方針**: `MakeMove` メソッドのDocコメントまたはメソッド内コメントに「MakeMove 内ではタイムアウトチェックを行ってはならない（パターンインデックスの差分更新がアトミックに完了する必要があるため）」を明記する。

**[P1-2] RFC 記載のデバッグカウンタの実装**
- **出典**: Technical Quality Reviewer
- **内容**: RFC 7.3 節で定義されたデバッグビルド限定の計測カウンタ（`BitboardMoveGenCount`, `ZobristUpdateCount`, `ZobristFullScanCount`, `PatternDeltaUpdateCount`, `PatternDeltaAvgAffected`）が未実装である。
- **修正方針**: `#if DEBUG` で囲んだカウンタを `PvsSearchEngine` と関連クラスに追加し、探索完了時にログ出力する。RFC 7.3 節の表に記載の5つのカウンタを全て実装する。

**[P1-3] NpsBenchmarkTest の終盤局面テストデータの改善**
- **出典**: Technical Quality Reviewer
- **内容**: `NpsBenchmarkTest` の終盤局面は初期局面の `TurnCount` を 50 に変更しただけで、RFC 7.2 節の条件（空マス14以下）を満たす実際の終盤局面ではない。
- **修正方針**: 空マス14以下の実際の終盤局面（50手目前後のオセロ局面）をテストデータとして追加し、`NpsBenchmarkTest` で使用する。

## 記録のみ (P2)

- context.Mobility の更新が `MobilityAnalyzer.Analyze` から除去されており、RFC との差異があるが、責務分離として妥当な設計判断である。（出典: Approach Reviewer）
- `UpdateHash` のインターフェースが RFC（`Disc.Color turn`）と異なり `bool isBlackTurn` に変更されているが、機能的に同等であり設計上合理的である。（出典: Approach Reviewer）
- パターン変更バッファ（`_patternChangeBuffer`）のサイズが固定値 8,192 エントリであり、Release ビルドではアサーション無効化によるサイレント配列外アクセスの理論的リスクがあるが、実用上はオーバーフローしない十分なマージンが確保されている。（出典: Security & Risk Reviewer）
- `FeaturePatternExtractor.PreallocatedResults` が public 公開されており、内部バッファの直接参照を外部に露出しているが、現時点のシングルスレッド設計では受容可能である。将来的にリファクタリングの候補として記録する。（出典: Technical Quality Reviewer）

## 要判断（矛盾する指摘）

該当なし
