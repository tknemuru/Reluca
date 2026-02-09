## Security & Risk Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Approve

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **例外の閉じ込め設計**: `SearchTimeoutException` は `Search()` メソッド内の `catch` ブロックで確実に捕捉され、`ISearchEngine` の利用者に伝播しない安全な設計となっている。
- **エッジケースの防御**: `TimeAllocator.Allocate()` は残り時間が 0 以下の場合に `MinTimeLimitMs` を返し、`EstimateRemainingMoves()` は `Math.Max(..., 1)` で 0 除算を防止するなど、堅実な防御的プログラミングが実施されている。
- **Stopwatch のオーバーフロー安全性**: `Stopwatch.ElapsedMilliseconds` は `long` 型であり、`_nodesSearched` も `long` 型で定義されているため、ビット AND によるタイムアウトチェック条件でオーバーフローの懸念がない。
- **MPC 浅い探索時のタイムアウトチェック動作**: MPC の浅い探索中は `_currentDepth` が維持されるため、レイヤー 2 のタイムアウトチェックが有効に機能し、MPC 用の浅い探索が無制限に延長されるリスクがない。
- **安全マージンの確保**: `SafetyMarginRatio = 0.05`（5%）により、時間切れによる対局負けのリスクが緩和されている。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**P0**: 該当なし

**[P1-1] TimeLimitMs に負値を指定した場合のバリデーション不足**
- **対象セクション**: 5.2 SearchOptions の拡張
- **内容**: `SearchOptions` のコンストラクタで `timeLimitMs` に負値（例: `-100`）が渡された場合のバリデーションが存在しない。負値が設定されると、`PvsSearchEngine.Search()` のレイヤー 1 で `remainingMs` が負値となり、depth=1 完了直後に常に打ち切りが発生する。depth=1 の結果は返されるため致命的ではないが、意図しない動作となる。
- **修正の期待値**: `SearchOptions` のコンストラクタで `timeLimitMs` が負値の場合に `ArgumentOutOfRangeException` をスローするか、`Math.Max(timeLimitMs, 0)` でクランプするバリデーションを追加する。

**P2**: 該当なし
