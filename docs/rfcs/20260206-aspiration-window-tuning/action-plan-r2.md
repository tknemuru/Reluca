# レビュー統合アクションプラン（ラウンド 2）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 6 件（アクション対象: 3 件）
- **P2 件数**: 該当なし（ラウンド2では P0 + P1 限定）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) — 上位3件

**[P1-1] fail-low / fail-high の拡張ロジックの重複排除**
- **出典**: Quality
- **内容**: `AspirationRootSearch()` の fail-low ブロックと fail-high ブロックの拡張ロジック（`useExponentialExpansion` の分岐含む）が完全に同一のコードであり、保守時の変更漏れリスクがある。
- **修正方針**: 拡張ロジックを `ExpandDelta(long delta, int retryCount, bool useExponentialExpansion)` のようなプライベートメソッドに抽出し、fail-low / fail-high の両方から呼び出す構造に変更する。

**[P1-2] ClampDelta の factor = 0 に対する防御的プログラミング**
- **出典**: Security
- **内容**: `ClampDelta` メソッドで `factor` が 0 の場合、`MaxDelta / factor` でゼロ除算が発生する。現在の呼び出し元では factor は最小 2 であるため問題は起きないが、メソッドが `private static` として独立しており、将来の呼び出し元追加時にゼロ除算のリスクがある。
- **修正方針**: `ClampDelta` メソッドの先頭に `Debug.Assert(factor > 0)` またはガード条件を追加する。

**[P1-3] フェーズ 0 のベースライン計測とフェーズ 1 の依存関係の明確化**
- **出典**: Approach
- **内容**: フェーズ 0 でベースライン計測を実施し、その結果をフェーズ 1 のステージ別 delta 初期値に反映する手順が示されているが、計測結果を RFC に反映するタイミングが不明確である。
- **修正方針**: フェーズ 0 完了後に計測結果を Section 5.1.4 に追記し、delta 初期値を確定させてからフェーズ 1 に進む旨を明記する。

### アクション対象外の P1（記録のみ）

- MPC ON 時に retry 発生率が悪化した場合の対応方針が撤退基準に含まれていない。MPC ON 専用の delta テーブル検討等の対応を追加すべきである（出典: Approach）
- 全面リバート時に `AspirationParameterTable` 等のデッドコードが残存するリスクがある。一定期間以内にデッドコード除去を検討する旨を注記すべきである（出典: Security）
- `DefaultDelta = 50` の意味と用途（テーブル範囲外のステージ用フォールバック値）をコメントで明記すべきである（出典: Quality）

## 記録のみ (P2)

該当なし（ラウンド2では P0 + P1 限定）

## 要判断（矛盾する指摘）

該当なし
