# レビュー統合アクションプラン（ラウンド 2）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 2 件（アクション対象: 2 件）
- **P2 件数**: 該当なし（ラウンド2では P0 + P1 限定）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) -- 上位3件

**[P1-1] PatternIndexChange バッファの再帰ネスト安全性に必要な具体値の定義**
- **出典**: Security (P1-1)
- **内容**: 5.4.4 にて「単一の大きな配列（`PatternIndexChange[MaxDepth * MaxChangesPerMove]`）を確保し、`MoveInfo` には開始オフセットと件数を保持する」と記載されているが、`MaxDepth` と `MaxChangesPerMove` の具体的な値が定義されていない。実装時に不適切なバッファサイズを設定すると、深い探索でバッファオーバーフローが発生するリスクがある。
- **修正方針**: `MaxDepth`（想定される最大探索深さ、例: 64 -- オセロの最大手数）と `MaxChangesPerMove`（1 手あたりの最大パターン変更数、例: 128 -- MoveInfo のバッファサイズと整合）の具体的な値を定義し、合計バッファサイズの上限（例: `64 * 128 = 8,192` エントリ）を 5.4.4 に追記する。

**[P1-2] Null Window Search 再探索時のハッシュ再利用の根拠を明示**
- **出典**: Quality (P1-1)
- **内容**: 5.2.5 の Pvs コード例内コメント「Null Window Search の再探索でも childHash を再利用（同一の子ノードのため）」が、再探索フローにおける MakeMove/UnmakeMove の実行回数に対する前提を明示していない。再探索フローで MakeMove が 1 回のみ実行される（UnmakeMove → MakeMove を繰り返さない）ことが `childHash` 再利用の前提であるが、この前提が設計文書に記載されていない。
- **修正方針**: 5.2.5 の Pvs コード例において、Null Window Search の再探索フロー（PVS パターン: MakeMove → Null Window Pvs → fail high の場合にフルウィンドウ Pvs を同一 childHash で再呼び出し → UnmakeMove）を補足コメントまたは設計ノートとして追記し、`childHash` 再利用の正当性を明確にする。

## 記録のみ (P2)

該当なし（ラウンド2では P0 + P1 限定）

## 要判断（矛盾する指摘）

該当なし
