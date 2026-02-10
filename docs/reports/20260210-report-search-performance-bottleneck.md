# 終盤探索性能ボトルネック調査・対応報告

## 概要

WZebra 組み込み（`PvsSearchEngine` 導入）後、終盤完全読みの探索時間が旧 `CachedNegaMax` 比で約100倍に劣化していた問題を調査・修正した。

| 指標 | 修正前 | 修正後 |
|---|---|---|
| 空き15マス（TurnCount=49）探索時間 | 8分24秒 | **4.9秒** |
| 探索エンジン | PvsSearchEngine（MPC有効） | PvsSearchEngine（MPC無効） |

## 発生経緯

- RFC `20260209-fix-endgame-search-freeze` の実装中、バックグラウンドテスト `G1_ターン46以降で終盤モードに切り替わる` が 8分24秒かかっていることが判明
- 旧 `CachedNegaMax` では同等の終盤局面を数秒で完全読みできていたため、WZebra 組み込みに起因する性能劣化と判断

## 原因分析

### 調査した仮説

| 仮説 | 結果 |
|---|---|
| 終盤の評価関数が DiscCountEvaluator → FeaturePatternEvaluator に変わった | **否定** — git 履歴で確認。組み込み前後とも DiscCountEvaluator を使用 |
| 探索エンジンの変更（CachedNegaMax → PvsSearchEngine）に伴う WZebra 由来オーバーヘッド | **肯定** — 後述 |

### 特定された根本原因：終盤での Multi-ProbCut (MPC) 有効化

`FindBestMover.cs` で終盤探索時にも `useMultiProbCut: true` が指定されていた。

MPC は `FeaturePatternEvaluator` の統計モデル（回帰パラメータ a, b, sigma）に基づいてカットを判定するが、終盤の `DiscCountEvaluator`（0〜64 の石数を返す）ではスケールが全く異なるため：

1. **カット条件がほぼ成立しない** — 浅い探索の結果と統計パラメータの不一致により、カット判定が常に失敗
2. **浅い探索コストが純粋なオーバーヘッドになる** — 各ノードで最大3ペアの浅い探索（depth 2/6, 4/10, 6/14）が走るが、カットが成立しないため枝刈り効果ゼロ
3. **オーバーヘッドが全ノードで発生** — 探索木全体に渡って不要な計算が蓄積し、100倍レベルの劣化に繋がった

### 他の調査項目

| 項目 | 影響度 | 状態 |
|---|---|---|
| 反復深化上限 EndgameDepth=99 → emptyCount | 補助的 | RFC で修正済 |
| 連続パス未検出（探索木の指数的膨張） | 最大 | RFC で修正済 |
| 終盤でのパターン差分更新 | 中 | RFC で修正済 |
| **MPC が終盤でも有効** | **中〜高** | **本対応で修正** |
| Aspiration Window（終盤でも有効） | 低 | 未対応（delta=30 は石数スケールに概ね適合） |
| OrderMoves のコスト | 低 | 未対応 |

## 修正内容

### 変更ファイル

**`Reluca/Movers/FindBestMover.cs`**（1行変更）

```diff
+            bool isEndgame = context.TurnCount >= EndgameTurnThreshold;
             var options = new SearchOptions(
                 depth,
                 useTranspositionTable: true,
                 useAspirationWindow: true,
                 aspirationUseStageTable: true,
-                useMultiProbCut: true,
+                useMultiProbCut: !isEndgame,
                 timeLimitMs: timeLimitMs
             );
```

**`Reluca.Tests/Search/PvsSearchEngineEndgameFixUnitTest.cs`**（テスト追加）

- `統合_空き15マスの終盤探索が実用的な時間で完了する`: TurnCount=49（空き15マス）の局面で 30 秒以内の完了を検証

### 探索ログ比較（空き15マス、修正後）

```
Depth  1:     7 nodes,   49ms
Depth  5:   686 nodes,    1ms
Depth 10: 36708 nodes,  205ms
Depth 15: 168401 nodes, 677ms
合計: 805,258 nodes, 4,901ms
MpcCuts: 0（MPC無効）
```

## 検証結果

- 全186テスト PASS（新規1テスト含む、回帰なし）
- 空き15マスの終盤探索: **4.9秒**（旧 CachedNegaMax 同等の数秒レベルを達成）

## 関連

- RFC: `docs/rfcs/20260209-fix-endgame-search-freeze/rfc.md`
- PR: https://github.com/tknemuru/Reluca/pull/63
- コミット: `71955e4`
