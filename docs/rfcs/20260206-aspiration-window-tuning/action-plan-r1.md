# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 3 件
- **P1 件数**: 5 件（アクション対象: 5 件）
- **P2 件数**: 3 件（記録のみ）

## 必須対応 (P0)

**[P0-1] 指数拡張戦略のフラグ制御範囲の曖昧さ**
- **出典**: Approach
- **内容**: `AspirationUseStageTable` フラグが delta 初期値のステージ別テーブル切替のみに使用されているが、指数拡張戦略（`delta * (1L << (retryCount + 1))`）は `AspirationUseStageTable` の ON/OFF に関係なく常に適用されるコードになっている。Section 7.3 の「`AspirationUseStageTable = false` の場合、動作は完全に同一」という記述と矛盾する。
- **修正方針**: `AspirationUseStageTable = false` の場合は従来の `delta *= 2` を使用し、`true` の場合のみ指数拡張を適用するように設計を修正する。あるいは指数拡張を独立したフラグ（例: `AspirationUseExponentialExpansion`）で制御する設計に変更する。

**[P0-2] ステージ別 delta 初期値の根拠不足**
- **出典**: Approach
- **内容**: 序盤 80、中盤 50、終盤 30 という delta 初期値は「仮設定」と明記されているが、Reluca の評価関数の出力スケール（数百〜数万）に対してこれらの値が妥当であるかの定量的分析がない。初期値が桁違いに不適切であった場合、ベンチマーク自体が有効な知見を得られない。
- **修正方針**: 現行パラメータ（delta = 50）での retry 発生率・フォールバック率のベースライン計測結果を RFC に追記し、初期値設定の出発点として使用する。

**[P0-3] 深さ補正係数の浮動小数点演算による精度損失**
- **出典**: Quality
- **内容**: `GetDepthFactor` が `double` を返し `(long)(baseDelta * depthFactor)` でキャストしている。1.5 倍の場合に奇数の baseDelta で切り捨てが発生するが、この動作が意図的であるか不明瞭である。探索エンジンのホットパスで浮動小数点演算を行うことは非決定的な動作のリスクがある。
- **修正方針**: `GetDepthFactor` を廃止し、`GetAdjustedDelta(long baseDelta, int depth)` として整数演算で直接補正後 delta を返すメソッドに変更する（例: depth <= 4 で `baseDelta * 3 / 2`、depth <= 2 で `baseDelta * 2`）。

## 推奨対応 (P1) — 上位5件

**[P1-1] フェーズ 3 ベンチマークの撤退基準の明確化**
- **出典**: Security
- **内容**: フェーズ 3 のベンチマークで改善が得られなかった場合の判断基準と対応方針が明示されていない。
- **修正方針**: フェーズ 3 に「撤退基準」を追加し、NodesSearched が悪化した場合のフォールバック手順（`AspirationUseStageTable = false` への全面リバート等）を明記する。

**[P1-2] 成功時の再探索コストに関する注記追加**
- **出典**: Approach
- **内容**: コスト分析表が再探索の実コストを過大に見積もっている。成功時の再探索は TT にヒットするため、初回探索と比較して大幅に軽量である。
- **修正方針**: Section 5.4.1 のコスト分析に「成功時の再探索は TT ヒットにより初回比で大幅に軽量である」旨の注記を追加する。

**[P1-3] 指数拡張の比較表に累積倍率列を追加**
- **出典**: Approach
- **内容**: 比較表の各 retry 時点の乗数と累積乗数の混同が生じやすい。
- **修正方針**: Section 5.3.2 の比較表に「累積倍率」列を追加し、各 retry 時点での delta の初期値比を明示する。

**[P1-4] AspirationParameterTable の Dictionary を配列に変更**
- **出典**: Quality
- **内容**: ステージは 1〜15 の固定範囲であり、`Dictionary<int, long>` を使用する必然性がない。`long[]` 配列で十分であり、GC 圧力の削減とキャッシュ効率の向上が見込める。
- **修正方針**: `_deltaByStage` を `long[]` 配列（サイズ 15、インデックス = stage - 1）に変更し、`GetDelta` メソッドをインデックスアクセスに変更する。

**[P1-5] retry カウンタリセットタイミングの修正**
- **出典**: Quality
- **内容**: `_aspirationRetryCount` と `_aspirationFallbackCount` のリセットが `AspirationRootSearch` メソッドの先頭で行われているが、Aspiration Window を使用しない depth=1 の場合にカウンタが初期化されない。
- **修正方針**: リセットを `Search` メソッドの depth ループ先頭（`_nodesSearched = 0` と同じ位置）に移動する。

## 記録のみ (P2)

- MPC との相互作用の検証優先度: MPC が Aspiration Window の retry 発生率に与える影響の方向性についての仮説を記述しておくと、ベンチマーク結果の解釈が容易になる（出典: Approach）
- retry 計測フィールドのスレッドセーフティ: 並列探索導入時には `_aspirationRetryCount` 等のスレッドセーフティが課題となる。現時点では Non-Goals 記載により対応不要（出典: Security）
- ステージ境界での delta の不連続変化: ステージ 5→6 で 80→50、10→11 で 50→30 と不連続に変化する。将来的な改善として線形補間や段階的遷移を検討する余地がある（出典: Quality）

## 要判断（矛盾する指摘）

該当なし
