## Security & Risk Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Approve

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **例外安全性の維持**: MakeMove/UnmakeMove パターンが try/finally で保護されており、タイムアウト例外発生時にもパターンインデックスとハッシュ値の整合性が復元される設計になっている。
- **デバッグアサーションの活用**: パターン変更バッファのオーバーフロー防止に `Debug.Assert` を使用しており、開発時のバグ早期検出に寄与する。
- **フォールバック設計**: `IncrementalMode` フラグにより差分更新とフルスキャンを切り替え可能であり、タイムアウト例外後にフルスキャンで一貫性を回復する設計が実装されている。
- **機密情報の取り扱い**: 本変更はパフォーマンス最適化のみであり、機密情報やユーザーデータを扱う箇所はなく、セキュリティ上の懸念はない。
- **依存ライブラリの安全性**: 新規依存は `System.Numerics.BitOperations`（.NET 標準ライブラリ）のみであり、既知の脆弱性リスクはない。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**P0**: 該当なし

**[P1-1] タイムアウト復元時の _patternChangeOffset 初期化漏れの可能性**
- **対象セクション**: PvsSearchEngine.cs（Search メソッドの SearchTimeoutException ハンドラ）
- **内容**: タイムアウト発生時に `_featurePatternExtractor.IncrementalMode = false` → `ExtractNoAlloc` → `IncrementalMode = true` → `_patternChangeOffset = 0` でリセットしているが、MakeMove の途中（`UpdatePatternIndicesForSquare` のループ中）でタイムアウト例外が発生した場合、`_patternChangeOffset` が中途半端に進んだ状態になる。フルスキャン後に `_patternChangeOffset = 0` に戻す処理はあるが、この流れが正しく機能するには `SearchTimeoutException` が `UpdatePatternIndicesForSquare` 内ではなく `Pvs` 再帰内の `_nodesSearched` チェック時にのみ発生することが前提である。現行実装では `MakeMove` 内で例外は発生しない（`ThrowIfTimeout` は `Pvs` 冒頭でのみ呼ばれる）ため実害はないが、将来的に `MakeMove` 内にタイムアウトチェックが追加された場合のリスクがある。
- **修正の期待値**: `MakeMove` 内ではタイムアウトチェックを行わないことをコメントで明記し、将来の改修者が誤ってチェックを追加しないよう注意喚起する。

**[P2-1] パターン変更バッファのサイズが固定値で動的拡張不可**
- **対象セクション**: PvsSearchEngine.cs（MaxSearchDepth * MaxChangesPerMove）
- **内容**: `_patternChangeBuffer` のサイズは `64 * 128 = 8,192` エントリの固定値であり、`Debug.Assert` で上限チェックされている。Release ビルドではアサーションが無効となり、バッファオーバーフロー時にサイレントに配列外アクセスが発生する可能性がある。ただし、RFC の分析（最大 11 マス x 8 パターン = 88 変更/手、最大 64 手深さ）に基づき、理論上のバッファ上限は 88 x 64 = 5,632 であり、8,192 で十分なマージンが確保されている。
- **修正の期待値**: 実用上オーバーフローは発生しないと判断できるため、現状の設計で問題はない。将来パターン数が大幅に増加する場合は動的拡張の検討が必要となる。
