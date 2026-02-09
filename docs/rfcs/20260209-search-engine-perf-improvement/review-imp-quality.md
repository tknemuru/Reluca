## Technical Quality Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Request Changes

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **stackalloc の活用**: OrderMoves で `Span<long>` / `Span<int>` を stackalloc で確保しており、ヒープアロケーションを効果的に排除している。
- **MoveInfo 構造体の設計**: `private struct MoveInfo` としてスタック上に配置される設計であり、MakeMove/UnmakeMove ペアごとのヒープアロケーションがゼロである。
- **Docコメントの充実**: すべてのメソッド・フィールドに日本語 Docコメントが記載されており、コード規約に準拠している。
- **既存 Extract メソッドの保持**: ExtractNoAlloc を新規追加しつつ既存の Extract メソッドを残しており、後方互換性が維持されている。
- **コードベースとの一貫性**: 既存の try/finally パターン（MobilityAnalyzer の Turn 復元）と一貫した方式で MakeMove/UnmakeMove の例外安全性を実現している。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**[P0-1] OrderMoves 内の BoardAccessor.Pass が UnmakeMove で正しく復元されない可能性**
- **対象セクション**: 5.1.3 OrderMoves の LINQ 排除 / 5.2.1 MakeMove/UnmakeMove パターン
- **内容**: OrderMoves 内で `MakeMove` 後に `BoardAccessor.Pass(context)` を呼んでいる。`Pass` は `context.Turn` を反転する。その後 `UnmakeMove` で `PrevTurn`（MakeMove 直前の Turn）に復元される。`MakeMove` 内で `NextTurn` により Turn が変更され、さらに `Pass` で Turn が再度反転されるが、`UnmakeMove` は `MakeMove` 直前の Turn に復元するため、`Pass` による変更も含めて正しく復元される。しかし、`Pass` は `context.Turn` 以外のフィールド（例: `TurnCount`）を変更しないか確認が必要である。`BoardAccessor.Pass` の実装を確認すると Turn のみ変更しているため、現在の実装では問題ないが、`NextTurn` が Turn 以外のフィールド（TurnCount, Stage）も変更している場合、MakeMove で保存した状態と Pass 後の状態に不整合が生じる。`BoardAccessor.NextTurn` の実装を確認し、MoveInfo に保存されるフィールドが完全に復元可能であることを明示的に検証すべきである。
- **修正の期待値**: `BoardAccessor.NextTurn` が変更するフィールドの一覧を確認し、MoveInfo が全フィールドをカバーしていることをコメントまたはアサーションで明示する。

**[P1-1] OrderMoves で Phase 2 の in-place ソート方式が未適用**
- **対象セクション**: 5.1.3 OrderMoves の LINQ 排除
- **内容**: RFC 5.1.3 の「Phase 2 との整合性」セクションで、Phase 2 移行時に `OrderMovesInPlace` として moves リストを in-place でソートし、`new List<int>(count)` のアロケーションを排除する設計が記載されているが、現在の実装は Phase 1 の方式のままである。Phase 2 の他の変更（MakeMove/UnmakeMove、NWS 等）は実装済みであるため、OrderMoves も Phase 2 方式に変更すべきである。
- **修正の期待値**: OrderMoves を in-place ソート方式に変更し、戻り値の `new List<int>(count)` アロケーションを排除する。

**[P1-2] FeaturePatternExtractor の Initialize メソッドで _preallocatedResults が再初期化されない**
- **対象セクション**: 5.1.4 FeaturePatternExtractor.Extract のアロケーション排除
- **内容**: `Initialize` メソッドで `PatternPositions` を上書き更新しているが、`_preallocatedResults` は再初期化されない。Initialize 呼び出し後に ExtractNoAlloc を使用すると、_preallocatedResults のキーやサイズが PatternPositions と不整合になる可能性がある。
- **修正の期待値**: Initialize メソッド内で `_preallocatedResults` も再構築するか、Initialize 後に ExtractNoAlloc が正しく動作する保証をコメントで明記する。

**[P2-1] RestoreContext と UnmakeMove の重複実装**
- **対象セクション**: 5.2.1 MakeMove/UnmakeMove パターンへの移行
- **内容**: `RestoreContext` と `UnmakeMove` は同一のロジック（MoveInfo の各フィールドを context に書き戻す）を持つが、別メソッドとして実装されている。メソッド名のセマンティクス（復元 vs 着手取り消し）は異なるため分離する設計判断は理解できるが、将来 MoveInfo にフィールドが追加された場合に片方のみ更新されるリスクがある。
