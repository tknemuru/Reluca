## Security & Risk Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Approve

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **try/finally による確実な復元**: MakeMove/UnmakeMove ペアがすべての呼び出し箇所で try/finally により保護されており、例外発生時にも盤面の不整合が発生しないよう設計されている。
- **ルート盤面バックアップによる二重防御**: SearchTimeoutException 発生時のフォールバックとして RestoreContext が実装されており、深い再帰からの脱出時にも安全性が担保されている。
- **副作用の明示的な文書化**: AnalyzeCount メソッドの Docコメントに、MoveAndReverseUpdater.Update の analyze モードによる副作用なしの根拠が明記されている。
- **シングルスレッド前提の明示**: ExtractNoAlloc の Docコメントにシングルスレッド前提の制約とマルチスレッド環境での対処方法が明記されており、将来的なリスクが可視化されている。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**P0**: 該当なし

**[P1-1] Pvs メソッド内のパス処理で盤面復元が欠如している**
- **対象セクション**: 5.2.1 MakeMove/UnmakeMove パターンへの移行
- **内容**: Pvs メソッドの合法手が 0 件の場合のパス処理（`BoardAccessor.Pass(context)`）では、context.Turn を直接変更しているが、再帰呼び出し後の Turn 復元を行っていない。パス時は `BoardAccessor.Pass` で Turn を反転し、`-Pvs(context, remainingDepth - 1, -beta, -alpha, true)` を呼ぶが、再帰から戻った後に Turn を元に戻す処理がない。ただし、Pvs 呼び出し後に context の状態を参照する処理（TT Store で `moves.Count > 0` のガードにより TT Store はスキップされる）がないため、現時点では実害は発生しない。しかし、将来の変更でパス後に context を参照する処理が追加された場合にバグとなるリスクがある。
- **修正の期待値**: パス処理にも try/finally による Turn の保存・復元を追加し、一貫したパターンを適用する。

**[P2-1] DoNotParallelize 属性のテスト実行速度への影響**
- **対象セクション**: テスト戦略
- **内容**: ExtractNoAlloc のシングルスレッド前提により、複数のテストクラスに `[DoNotParallelize]` 属性が追加されている。テストスイートの規模が拡大した場合、テスト実行時間に影響する可能性がある。現時点では問題ないが、テスト数が増加した際にはテスト専用の FeaturePatternExtractor インスタンスをテストごとに生成する方式への移行を検討する余地がある。
