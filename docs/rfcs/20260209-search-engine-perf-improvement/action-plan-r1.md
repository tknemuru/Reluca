# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 6 件（アクション対象: 5 件）
- **P2 件数**: 5 件（記録のみ）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) — 上位5件

**[P1-1] UnmakeMove の例外発生時における状態復元保証の設計追加**
- **出典**: Security & Risk Reviewer
- **内容**: MakeMove/UnmakeMove パターンでは、探索中に SearchTimeoutException 等の例外が発生した場合に UnmakeMove が呼ばれず盤面が不整合状態になるリスクがある。現行の DeepCopy 方式にはないリスクが新たに発生する。
- **修正方針**: Phase 2 の MakeMove/UnmakeMove 設計（セクション 5.2.1）に、try/finally による UnmakeMove 呼び出し保証、または探索開始前のルート盤面バックアップによるフォールバック機構を追記する。

**[P1-2] Phase 1 完了時の Go/No-Go 判定基準の定量化**
- **出典**: Approach Reviewer
- **内容**: Phase 1 完了後に Phase 2 に進むかどうかの判断基準が「効果が十分であれば」という曖昧な表現にとどまっており、定量的な閾値が設定されていない。
- **修正方針**: セクション 9 のリスク軽減策に、Phase 1 完了時の Go/No-Go 判定基準として GC 回数削減率や探索時間の具体的な数値目標を追記する（例: 「GC Gen0 コレクション回数が 50% 以上削減されなければ Phase 2 を優先実施」等）。

**[P1-3] AnalyzeCount メソッドにおける MoveAndReverseUpdater.Update の副作用の明確化**
- **出典**: Technical Quality Reviewer
- **内容**: AnalyzeCount のコード例では `_updater.Update(context, i)` を呼び出しているが、Update が context に副作用（盤面変更）を与えるか否かが RFC 内で明記されていない。副作用がある場合のロールバック処理設計が不足している。
- **修正方針**: セクション 5.1.2 に、MoveAndReverseUpdater.Update の着手可能判定時の副作用の有無を明記する。副作用がある場合はロールバック処理を設計に追加し、ない場合はその旨を仮定として記載する。

**[P1-4] OrderMoves の Phase 1 実装と Phase 2 MakeMove/UnmakeMove パターンの整合性記述**
- **出典**: Approach Reviewer / Technical Quality Reviewer（関連指摘を統合）
- **内容**: Phase 1 の OrderMoves 改善（5.1.3）では DeepCopy 方式の MakeMove を前提としたコード例が記載されているが、Phase 2 で MakeMove/UnmakeMove に移行した場合の OrderMoves の変更方針が不明確である。また、改善後も `new List<int>(count)` による新規リストアロケーションが残存している。
- **修正方針**: セクション 5.1.3 に、Phase 2 移行時に OrderMoves が MakeMove/UnmakeMove パターンに対応し、かつ moves リストの in-place ソートにより残存アロケーションも排除する方針である旨を簡潔に追記する。

**[P1-5] ExtractNoAlloc の内部バッファ共有に伴うスレッド安全性の制約明記**
- **出典**: Security & Risk Reviewer
- **内容**: シングルスレッド前提で内部バッファを共有する設計であるが、将来的なマルチスレッド化時にデータ競合リスクがある。現時点では問題ないが、制約をコードとドキュメントに明記すべきである。
- **修正方針**: セクション 5.1.4 の設計に、ExtractNoAlloc が「シングルスレッド専用」であることを明記し、マルチスレッド化時には ThreadLocal バッファまたはインスタンス分離が必要である旨を注記する。

以下の 1 件は優先度により省略した:
- Approach Reviewer P1-2「OrderMoves の Phase 1/Phase 2 整合性」は P1-4 に統合済み。

## 記録のみ (P2)

- Aspiration 二重探索排除の仮定（TT Store 抑制不要）に対する検証テストケースを具体的に設計しておくと、Phase 2 実装時の品質保証に寄与する（出典: Approach Reviewer）
- Phase 2 のステップ 2-1〜2-4 間の依存関係（実施順序の柔軟性）を明確化しておくと、実装着手時の判断が容易になる（出典: Approach Reviewer）
- record struct 化で将来的にフィールド追加された場合のコピーコスト増大リスク、および意図しないボクシングによるアロケーション増加の可能性がある（出典: Security & Risk Reviewer）
- Phase 2 のロールバック手順は RFC-driven Workflow のセクション 4 で一般的な手順が定義されているが、MakeMove/UnmakeMove + record struct 化の組み合わせに特有のロールバック考慮事項を記録しておくと有用である（出典: Security & Risk Reviewer）
- Null Window Search の re-search 発生率を計測するメトリクス設計（カウンタフィールド、ログ出力フォーマット）を事前に定義しておくと、Move Ordering の品質を継続的に評価できる（出典: Technical Quality Reviewer）

## 要判断（矛盾する指摘）

該当なし
