# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 1 件
- **P1 件数**: 3 件（アクション対象: 3 件）
- **P2 件数**: 4 件（記録のみ）

## 必須対応 (P0)

**[P0-1] OrderMoves 内の BoardAccessor.Pass が MoveInfo の復元範囲で完全にカバーされていることの検証**
- **出典**: Technical Quality Reviewer
- **内容**: OrderMoves 内で MakeMove 後に `BoardAccessor.Pass(context)` を呼んでいる。`Pass` は Turn を反転し、`UnmakeMove` は MakeMove 直前の状態に復元するため、Pass による変更も含めて復元される。しかし、`BoardAccessor.NextTurn` が Turn 以外のフィールド（TurnCount, Stage 等）を変更している場合、MoveInfo に保存されるフィールドで完全に復元可能であるかの検証が明示されていない。
- **修正方針**: `BoardAccessor.NextTurn` が変更するフィールドの一覧を確認し、MoveInfo が全フィールドをカバーしていることをコメントまたはアサーションで明示する。カバーされていないフィールドがあれば MoveInfo に追加する。

## 推奨対応 (P1) — 上位5件

**[P1-1] Pvs メソッドのパス処理で Turn 復元が欠如している**
- **出典**: Security & Risk Reviewer
- **内容**: Pvs メソッドの合法手 0 件時のパス処理で、`BoardAccessor.Pass(context)` により Turn を変更した後、再帰呼び出しから戻った際に Turn を復元する処理がない。現時点では実害は発生しないが、将来のコード変更でバグとなるリスクがある。
- **修正方針**: パス処理にも try/finally による Turn の保存・復元を追加し、MakeMove/UnmakeMove パターンと一貫した例外安全性を確保する。

**[P1-2] OrderMoves で Phase 2 の in-place ソート方式が未適用**
- **出典**: Technical Quality Reviewer, Approach Reviewer
- **内容**: RFC 5.1.3 の「Phase 2 との整合性」で設計された `OrderMovesInPlace` 方式（moves リストを in-place でソートし `new List<int>(count)` を排除する方式）が、Phase 2 の他の変更が実装済みであるにもかかわらず未適用である。
- **修正方針**: OrderMoves を in-place ソート方式に変更し、戻り値の `new List<int>(count)` アロケーションを排除する。

**[P1-3] FeaturePatternExtractor.Initialize で _preallocatedResults が再初期化されない**
- **出典**: Technical Quality Reviewer
- **内容**: Initialize メソッドで PatternPositions を上書き更新しているが、_preallocatedResults は再初期化されない。Initialize 呼び出し後に ExtractNoAlloc を使用すると不整合が発生する可能性がある。
- **修正方針**: Initialize メソッド内で _preallocatedResults も PatternPositions に基づいて再構築する。

## 記録のみ (P2)

- OrderMoves 内の BoardAccessor.Pass は Turn のみ変更しており、UnmakeMove で MakeMove 前の Turn に復元されるため現状は安全である（出典: Technical Quality Reviewer P0-1 の補足）
- RestoreContext と UnmakeMove は同一ロジックの重複実装だが、セマンティクスの違いによる分離は妥当。将来の MoveInfo フィールド追加時に片方のみ更新されるリスクに留意する（出典: Technical Quality Reviewer）
- DoNotParallelize 属性によるテスト実行速度への影響は、テスト数増加時に再検討する（出典: Security & Risk Reviewer）
- RootSearch での NWS 未適用はルート探索効率のさらなる向上余地として記録する（出典: Approach Reviewer）

## 要判断（矛盾する指摘）

該当なし
