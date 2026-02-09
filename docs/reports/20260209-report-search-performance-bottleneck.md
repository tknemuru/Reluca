# 探索エンジン パフォーマンスボトルネック調査レポート

- **日付**: 2026-02-09
- **種別**: report
- **対象**: WZebra評価テーブル組み込み後の探索速度低下

## 背景

Windows環境で実際に対局した結果、WZebra評価テーブル組み込み前の旧ロジックと比較して大幅に遅くなっている。原因を調査し、改善策をまとめる。

## 調査結果

### ボトルネック一覧（優先順位順）

| 優先度 | 問題 | 場所 | 頻度 |
|--------|------|------|------|
| P0 | MakeMoveで毎回DeepCopy | `PvsSearchEngine.cs:798-805` | 全探索ノード × (1 + Move Ordering内) |
| P0 | MobilityAnalyzer.Analyzeが64マス全スキャン + 毎回Listアロケーション | `MobilityAnalyzer.cs:53-65` | 全探索ノードで3回/ノード |
| P0 | FeaturePatternExtractor.Extractで毎回Dictionary + List×11アロケーション | `FeaturePatternExtractor.cs:51-63` | 全Evaluate呼び出し |
| P0 | OrderMoves内のLINQ + MakeMove + Evaluateの組み合わせ | `PvsSearchEngine.cs:829-842` | 大半の探索ノード |
| P1 | PVSが実際にはNull Window Searchを使っていない（純粋NegaMax） | `PvsSearchEngine.cs:649-656` | 枝刈り効率の大幅な低下 |
| P1 | Aspiration成功時にRootSearchを2回実行 | `PvsSearchEngine.cs:387-401` | 各反復深化の深さごと |
| P1 | GameContext/BoardContextが`record`（参照型）でGC圧力 | `Contexts/GameContext.cs`, `BoardContext.cs` | 全DeepCopy |
| P2 | Zobristハッシュが毎回64マスフルスキャン | `ZobristHash.cs:33-64` | 全探索ノード |
| P2 | GetDiscCountが64回ループ（PopCount未使用） | `BoardAccessor.cs:296-310` | 終盤完全読み切り全ノード |
| P2 | DiProvider.Get().GetServiceを探索ホットパスで呼び出し | `MobilityAnalyzer.cs:54` | 全探索ノード × 3回 |
| P3 | List.Contains/Remove/InsertのO(n)操作 | `OptimizeMoveOrder`, `Pvs`内 | 全探索ノード |

### P0: MakeMoveで毎回DeepCopy

```csharp
// PvsSearchEngine.cs:798-805
private GameContext MakeMove(GameContext context, int move)
{
    var copyContext = BoardAccessor.DeepCopy(context);
    copyContext.Move = move;
    _reverseUpdater.Update(copyContext);
    BoardAccessor.NextTurn(copyContext);
    return copyContext;
}

// BoardAccessor.DeepCopy - record の with 式で毎回ヒープアロケーション
public static GameContext DeepCopy(GameContext context)
{
    return context with { Board = context.Board with { } };
}
```

- `GameContext`/`BoardContext` は `record`（参照型）であり、`with` 式のたびにヒープに新オブジェクトを生成
- 探索ノード数が数万〜数十万に達するため、同数のGCオブジェクトが生成される
- 通常のオセロAIは MakeMove/UnmakeMove パターン（in-place変更＋元に戻す）でアロケーションをゼロにする

### P0: MobilityAnalyzer.Analyzeの問題

```csharp
// MobilityAnalyzer.cs:53-65
var updater = DiProvider.Get().GetService<MoveAndReverseUpdater>(); // DIコンテナ毎回取得
var mobilitys = new List<int>(); // 毎回Listアロケーション
// 64マス全てに対して8方向whileループの合法手判定
```

- 毎回 `new List<int>()` をヒープにアロケーション
- 64マス全てに対して `MoveAndReverseUpdater.Update` を実行（8方向ループ）
- `Evaluate` 内で黒/白の2回 + `Pvs` 内で1回 = **1ノードあたり3回**呼ばれる
- 内部で `DiProvider.Get().GetService<MoveAndReverseUpdater>()` を毎回呼び出し

### P0: FeaturePatternExtractor.Extractのアロケーション

```csharp
// FeaturePatternExtractor.cs:51-63
public Dictionary<FeaturePattern.Type, List<int>> Extract(BoardContext context)
{
    var result = new Dictionary<FeaturePattern.Type, List<int>>();
    foreach (var pattern in PatternPositions)
    {
        result[pattern.Key] = new List<int>();
        // ...
    }
    return result;
}
```

- パターン11種類分の `Dictionary` + `List<int>` × 11 が毎回ヒープに生成される

### P0: OrderMoves内のLINQ + MakeMove + Evaluate

```csharp
// PvsSearchEngine.cs:829-842
private List<int> OrderMoves(List<int> moves, GameContext context)
{
    var ordered = moves
        .OrderByDescending(move =>
        {
            var child = MakeMove(context, move);
            BoardAccessor.Pass(child);
            return Evaluate(child);
        })
        .ToList();
    return ordered;
}
```

- `OrderByDescending` のラムダ内で各手についてDeepCopy → Evaluate（MobilityAnalyzer × 2 + FeaturePatternExtractor）をフル実行
- LINQのデリゲートオブジェクト + ソート用バッファ + `.ToList()` のアロケーション
- `ShouldOrder` の条件により探索木のほぼ全ノードで実行される

### P1: PVS/NegaScoutのNull Window Search未実装

```csharp
// PvsSearchEngine.cs:649-656 - 全手フルウィンドウで探索
foreach (var move in moves)
{
    var child = MakeMove(context, move);
    long score = -Pvs(child, remainingDepth - 1, -beta, -alpha, false);
    // ...
}
```

- 名前は `Pvs` だが、2手目以降も `(-beta, -alpha)` のフルウィンドウで探索
- 本来は2手目以降 `(-alpha-1, -alpha)` のNull Window Search → fail-high時のみre-search

### P1: Aspiration成功時のRootSearch二重実行

```csharp
// PvsSearchEngine.cs:387-401
_suppressTTStore = true;
var result = RootSearch(context, depth, alpha, beta);
if (result.Value <= initialAlpha || result.Value >= initialBeta)
{
    // retry ...
}
else
{
    _suppressTTStore = false;
    return RootSearch(context, depth, alpha, beta); // 2回目の探索
}
```

- TT Store有効化のために同じ探索を2回実行している

### GCオブジェクト生成量の推定

探索ノード10万ノードの場合の1ノードあたり主なアロケーション:

- `MakeMove`: GameContext + BoardContext = 2オブジェクト
- `OrderMoves`: 上記 × 合法手数(~8) + LINQ内部バッファ + List
- `Evaluate`: MobilityAnalyzer × 2 (List × 2) + FeaturePatternExtractor (Dict + List × 11)
- **1ノードあたり約30〜50オブジェクト** × 10万ノード = **300〜500万のGCオブジェクト**

これがGC圧力となりGen0/Gen1 GCが頻発して大幅な遅延を引き起こしている。

## 改善提案

### Phase 1: 即効性のある改善（変更範囲が限定的）

1. **DIコンテナ呼び出し排除**: `MobilityAnalyzer` で `DiProvider.Get().GetService` をやめ、コンストラクタインジェクションで `MoveAndReverseUpdater` を保持
2. **MobilityAnalyzer.Analyze のカウント専用メソッド追加**: Evaluate内では `Count` しか使わないので `AnalyzeCount` を用意し、`List<int>` のアロケーションを排除
3. **OrderMoves の LINQ 排除**: 固定長配列 + インラインソートに置き換え
4. **FeaturePatternExtractor.Extract のアロケーション排除**: 事前確保した配列にインデックスを書き込む方式に変更

### Phase 2: 構造的な改善（効果大・変更範囲大）

5. **MakeMove/UnmakeMove パターンへの移行**: 盤面をin-placeで変更し探索後に元に戻す。アロケーションゼロ
6. **BoardContext を `record struct` に変更**: 参照型→値型にしてスタックコピーに。GC圧力をゼロに
7. **PVS/NegaScout の Null Window Search 実装**: 2手目以降を `(-alpha-1, -alpha)` で探索し枝刈り効率を大幅改善
8. **Aspiration成功時の二重RootSearch排除**: `_suppressTTStore` フラグの切り替えで1回で完了させる

### Phase 3: さらなる高速化

9. **ビットボードによる高速合法手生成**: 64マスループをシフト演算＋マスクのO(1)に近い処理に置換
10. **Zobristハッシュの差分更新**: 着手時に変化したマスだけXOR
11. **`BitOperations.PopCount()` の利用**: 64回ループを1CPU命令に
12. **評価関数の差分更新**: パターンインデックスをMakeMove時に差分計算

## 根本原因のまとめ

WZebra評価テーブル組み込み自体が問題というより、**評価関数が重くなった結果、既存の構造的な非効率（毎回のヒープアロケーション、64マスフルスキャン、LINQ使用等）が顕在化した**のが本質的な原因。旧ロジックでは評価関数が軽量だったため問題が目立たなかっただけで、ボトルネックの構造は以前から存在していた。

## 次のアクション

- Phase 1 の実装を `/imp` で依頼（即効性あり・変更範囲限定的）
- Phase 2 は `/rfc` で設計してから `/imp`（影響範囲大のためRFC推奨）
- `dotnet-trace` や `dotnet-counters` でGC回数・アロケーション量を計測し定量的に確認してからPhase 2の優先順位を決定
