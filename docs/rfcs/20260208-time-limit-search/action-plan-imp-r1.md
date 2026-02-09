# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 2 件（アクション対象: 2 件）
- **P2 件数**: 3 件（記録のみ）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) — 上位5件

**[P1-1] レイヤー 2 タイムアウト発生時の totalNodesSearched 集計漏れ**
- **出典**: Technical Quality Reviewer
- **内容**: `catch (SearchTimeoutException)` ブロック内で `break` するため、タイムアウトが発生した深さの `_nodesSearched` が `totalNodesSearched` に加算されない。`SearchResult.NodesSearched` が実際の探索ノード数より少なく報告される。
- **修正方針**: `catch (SearchTimeoutException)` ブロック内の `break` の前に `totalNodesSearched += _nodesSearched;` を追加する。

**[P1-2] TimeLimitMs に負値を指定した場合のバリデーション不足**
- **出典**: Security & Risk Reviewer
- **内容**: `SearchOptions` のコンストラクタで `timeLimitMs` に負値が渡された場合のバリデーションがない。負値が設定されると depth=1 完了直後に常に打ち切りが発生し、意図しない動作となる。
- **修正方針**: `SearchOptions` のコンストラクタで `timeLimitMs` が負値の場合に `ArgumentOutOfRangeException` をスローするバリデーションを追加する。

## 記録のみ (P2)

- FindBestMover での RemainingTimeMs の消費更新が未実装。RFC の仮定通り外部から設定される前提のため現時点では問題ないが、将来的にプロトコル層からの自動設定時に注意が必要である（出典: Approach Reviewer）
- SearchOptions の MaxDepth と TimeLimitMs の同時指定時に「AND 条件」として動作する点は、利用者向けの注意事項として認識しておくべきである（出典: Approach Reviewer）
- Stopwatch の Stop/Restart のライフサイクルについて、`Stop()` と `return` の間にコードが挿入されても `ElapsedMilliseconds` に影響がない点は認識しておくべきである（出典: Technical Quality Reviewer）

## 要判断（矛盾する指摘）

該当なし
