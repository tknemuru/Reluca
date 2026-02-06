## Technical Quality Reviewer によるレビュー結果

### 1. 判定 (Decision)

- **Status**: Request Changes

**判定基準:** P0 が1件以上存在する場合は Request Changes とする。P0 が0件の場合は Approve とする。

### 2. 良い点 (Strengths)

- **Section 5.8 (DI 登録)**: `AspirationParameterTable` を Singleton として DI 登録する設計は、`MpcParameterTable` と同一のパターンであり、既存コードベースとの一貫性が保たれている。
- **Section 5.2.2 (深さ別 delta 補正)**: `GetDepthFactor` を static メソッドとして実装することで、テスト容易性が確保されている。
- **Section 5.5 (retry 計測機能)**: 既存のログ構造（`探索進捗 {@SearchProgress}`）への拡張であり、可観測性が追加コスト無しで向上する設計になっている。
- **Section 7.1 (パフォーマンス)**: テーブル引き $O(1)$、ビットシフト演算の採用など、探索ホットパスへのオーバーヘッドを最小化する設計が意識されている。

### 3. 指摘事項 (Issues)

#### Severity 定義

| Severity | 名称 | 定義 | Author の対応 |
| :--- | :--- | :--- | :--- |
| **P0 (Blocker)** | 修正必須 | 論理的欠陥、仕様漏れ、重大なリスク、回答必須の質問 | 必ず対応 |
| **P1 (Improvement)** | 具体的改善 | 修正内容と期待効果が明確な具体的改善提案 | 原則対応 |
| **P2 (Note)** | 記録のみ | 代替案の提示、将来的な懸念、参考情報 | 対応不要 |

#### 指摘一覧

**[P0-1] AspirationParameterTable の深さ補正係数が double 返却で精度損失の可能性**
- **対象セクション**: Section 5.2.2 (深さ別 delta 補正)
- **内容**: `GetDepthFactor` は `double` を返し、`(long)(baseDelta * depthFactor)` でキャストしている。現在の補正係数（2.0, 1.5, 1.0）では問題ないが、1.5 倍の場合に `baseDelta = 30` だと `(long)(30 * 1.5) = 45` となり、`(long)(31 * 1.5) = 46`（実際は 46.5 で切り捨て）となる。この切り捨て動作が意図的であるか不明瞭である。浮動小数点演算を避け、整数演算（分子/分母のペア、例: depth <= 4 で `baseDelta * 3 / 2`）に変更すべきである。
- **修正の期待値**: `GetDepthFactor` を整数ベースの補正に変更し（例: `GetAdjustedDelta(long baseDelta, int depth)` として直接補正後 delta を返す）、浮動小数点の切り捨てによる非決定的な動作を排除すること。

**[P1-1] AspirationParameterTable の Dictionary 使用は過剰**
- **対象セクション**: Section 5.2.1 (ステージ別 delta テーブルの導入)
- **内容**: ステージは 1〜15 の固定範囲であり、`Dictionary<int, long>` を使用する必然性がない。`long[]` 配列（インデックス = stage - 1）で十分であり、GC 圧力の削減とキャッシュ効率の向上が見込める。`MpcParameterTable` が Dictionary を使用しているのはカットペアインデックスとの二次元アクセスのためであり、一次元の stage-to-delta マッピングに Dictionary を使う根拠が薄い。
- **修正の期待値**: `_deltaByStage` を `long[]` 配列（サイズ 15）に変更し、`GetDelta` メソッドをインデックスアクセスに変更すること。

**[P1-2] AspirationRootSearch の retry カウンタリセットタイミング**
- **対象セクション**: Section 5.7 (PvsSearchEngine の変更)
- **内容**: `_aspirationRetryCount` と `_aspirationFallbackCount` のリセットが `AspirationRootSearch` メソッドの先頭で行われているが、反復深化のログ出力は `Search` メソッド内の各 depth ループで行われる。Aspiration Window を使用しない depth=1 の場合、これらのカウンタは初期化されない。`Search` メソッドの depth ループ先頭（`_nodesSearched = 0` と同じ位置）でリセットすべきである。
- **修正の期待値**: `_aspirationRetryCount` と `_aspirationFallbackCount` のリセットを `Search` メソッドの depth ループ先頭に移動すること。

**[P2-1] ステージ境界での delta の不連続変化**
- **対象セクション**: Section 5.2.1 (ステージ別 delta テーブルの導入)
- **内容**: ステージ 5→6 で delta が 80→50、ステージ 10→11 で 50→30 と不連続に変化する。ゲーム進行中にステージ境界をまたぐ局面で、前回反復と今回反復で異なるステージが適用された場合、delta の不連続変化により不必要な retry が発生する可能性がある。将来的な改善として線形補間や段階的な遷移を検討する余地がある。
