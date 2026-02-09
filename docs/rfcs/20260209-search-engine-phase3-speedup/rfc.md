# [RFC] 探索エンジン Phase 3 高速化（ビットボード・差分更新・ハードウェア命令活用）

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI (Claude) |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-09 |
| **タグ** | Search, Evaluates, Analyzers, Accessors, Transposition, Performance, Phase3 |
| **関連リンク** | `docs/reports/20260209-report-search-performance-bottleneck.md`, `docs/rfcs/20260209-search-engine-perf-improvement/rfc.md` (Phase 1 & 2 RFC) |

## 1. 要約 (Summary)

- 本 RFC は、探索エンジン パフォーマンス改善の Phase 3 として、ビットボード演算・ハードウェア命令・差分更新の 3 軸による高速化を提案する。
- 具体的には、(9) ビットボードによる高速合法手生成、(10) Zobrist ハッシュの差分更新、(11) `BitOperations.PopCount()` による石数カウント、(12) 評価関数の差分更新の 4 項目を実施する。
- Phase 1（GC アロケーション削減）および Phase 2（MakeMove/UnmakeMove パターン導入・Null Window Search 実装等）の完了を前提条件とし、それらの構造改善の上にハードウェアレベルの最適化を積み重ねる。
- これにより、合法手生成の計算量を O(64×8方向) から O(定数) に削減し、Zobrist ハッシュ計算を O(64) から O(変化マス数) に削減し、石数カウントを 64 回ループから 1 CPU 命令に置換し、評価関数のパターンインデックス計算を毎回フルスキャンから差分計算に変更する。

## 2. 背景・動機 (Motivation)

- WZebra 評価テーブル組み込み後に探索速度が大幅に低下し、その原因分析と改善計画を Phase 1〜3 に分けて策定した（`docs/reports/20260209-report-search-performance-bottleneck.md` 参照）。
- Phase 1 では DI コンテナ呼び出し排除、`AnalyzeCount` 追加、LINQ 排除、`ExtractNoAlloc` 導入により GC アロケーションを大幅に削減した。Phase 2 では MakeMove/UnmakeMove パターン、`BoardContext` の `record struct` 化、Null Window Search 実装、Aspiration 二重探索排除により構造的な非効率を解消した。
- Phase 1/2 の改善により GC 圧力は実質ゼロに近づき、探索ノードあたりのアロケーションコストは解消された。しかし、以下のアルゴリズムレベルの非効率が依然として残存している:
  - **合法手生成の O(64×8方向) ループ**: `MobilityAnalyzer.Analyze` および `AnalyzeCount` は 64 マス全てに対して 8 方向の while ループで合法手判定を行っている（`MobilityAnalyzer.cs`）。さらに `MoveAndReverseUpdater.Update` の内部実装（`MoveAndReverseUpdater.cs`）も各方向ごとに逐次的なインデックス操作で裏返し判定を行っている。ビットボード表現は既に導入済み（`BoardContext.Black`, `BoardContext.White` が `ulong`）であるにもかかわらず、ビット演算による一括処理が活用されていない。
  - **Zobrist ハッシュの毎回フルスキャン**: `ZobristHash.ComputeHash` は 64 マス全てをループして石の有無を確認する（`ZobristHash.cs:41-55`）。探索中はノードごとに 1〜2 回呼ばれるため、1 ノードあたり 64〜128 回の条件分岐とビットマスク操作が発生している。MakeMove/UnmakeMove パターン導入済みの現在、着手で変化するマスは数個であり、差分更新で計算量を劇的に削減できる。
  - **`GetDiscCount` の 64 回ループ**: `BoardAccessor.GetDiscCount` は 64 回のループとビットマスク判定で石数をカウントしている（`BoardAccessor.cs:296-310`）。.NET 8.0 では `System.Numerics.BitOperations.PopCount()` が利用可能であり、これはほとんどのモダン CPU で 1 命令（`POPCNT`）にコンパイルされる。
  - **評価関数のパターンインデックス毎回フルスキャン**: `FeaturePatternExtractor.ExtractNoAlloc` は毎回 13 種類のパターンの全マスを走査して 3 進数インデックスを計算している（`FeaturePatternExtractor.cs:96-107`）。着手で変化するマスは限定的であり、差分更新により影響を受けるパターンのインデックスのみを再計算できる。
- これらの最適化は Phase 1/2 の構造改善（特に MakeMove/UnmakeMove パターンと `BoardContext` の値型化）が前提である。差分更新は「前の状態からの変化量」を追跡する必要があり、in-place 更新パターンとの親和性が高い。

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- ビットボード演算による合法手生成の高速化: 64 マス × 8 方向の逐次ループを、シフト演算とビットマスクによる O(定数) の一括処理に置換する
- Zobrist ハッシュの差分更新: ノードあたりの計算量を O(64) から O(変化マス数) に削減する（通常 2〜10 マス程度）
- `BitOperations.PopCount()` の活用: `GetDiscCount` の 64 回ループを 1 CPU 命令に置換する
- 評価関数の差分更新: MakeMove 時にパターンインデックスを差分計算し、毎回のフルスキャンを排除する

### Phase 3 開始条件

Phase 3 の着手には以下の前提条件を満たしていることが必要である:

| 条件 | 確認方法 | 具体的な参照先 |
|------|---------|--------------|
| Phase 1 & Phase 2 の全項目が実装・マージ済みであること | Phase 1/2 の PR がマージされていること | RFC: `docs/rfcs/20260209-search-engine-perf-improvement/rfc.md`, PR: #58（RFC承認）, #59（実装マージ） |
| MakeMove/UnmakeMove パターンが正しく動作していること | 既存テスト全通過 + 実対局での動作確認 | `Reluca.Tests/Search/PvsSearchEngine*UnitTest.cs` の全テスト通過 |
| `BoardContext` が `record struct` であること | コードベースの確認 | `Reluca/Contexts/BoardContext.cs` |

### やらないこと (Non-Goals)

- マルチスレッド探索の導入（並列化は Phase 3 のスコープ外）
- 評価関数自体の精度・構造の変更（パターンの種類や重みの変更は行わない）
- Opening Book（定石データベース）の導入
- 終盤完全読み切り（Endgame Solver）の最適化（`PopCount` の導入は寄与するが、専用ソルバーの設計は行わない）
- SIMD（`Vector256` 等）による並列ビット演算の導入（将来課題とする）

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- .NET 8.0 / C# 12 環境であること（`System.Numerics.BitOperations.PopCount()` は .NET Core 3.0 以降で利用可能）
- Phase 1 & Phase 2 の全改善項目が実装・マージ済みであること（特に MakeMove/UnmakeMove パターン、`BoardContext` の `record struct` 化）
- 現行の盤面表現（`BoardContext.Black: ulong`, `BoardContext.White: ulong`）が維持されていること
- `ZobristKeys` の乱数テーブル構造（`PieceKeys[64, 2]`, `TurnKey`）が維持されていること
- `FeaturePatternExtractor` のパターン定義（13 種類、各パターンのマス位置情報）が維持されていること
- `IZobristHash` インターフェースに差分更新用メソッドを追加する必要がある（既存の `ComputeHash` は互換性のため残す）

## 5. 詳細設計 (Detailed Design)

### 5.1 ビットボードによる高速合法手生成

**対象ファイル**: `Reluca/Analyzers/MobilityAnalyzer.cs`, `Reluca/Updaters/MoveAndReverseUpdater.cs`（新規追加: `Reluca/Analyzers/BitboardMobilityGenerator.cs`）

#### 5.1.1 現状の問題

`MobilityAnalyzer.Analyze` / `AnalyzeCount` は 64 マス全てに対して `MoveAndReverseUpdater.Update(context, i)` を呼び出し、各マスで 8 方向の while ループを実行している。1 回の合法手生成あたり最悪 64 × 8 × 7 = 3,584 回のインデックス計算とビットマスク判定が発生する。

`MoveAndReverseUpdater.Update` の内部実装は各方向ごとに `BoardAccessor.GetColumnIndex()`, `BoardAccessor.GetRowIndex()`, `BoardAccessor.ExistsDisc()` 等のメソッド呼び出しを含み、仮想関数呼び出しこそないが、メソッド呼び出しのオーバーヘッドとブランチ予測ミスが累積する。

#### 5.1.2 ビットボード合法手生成アルゴリズム

8 方向（上下左右、4 対角線）のそれぞれについて、シフト演算で「相手石の連続列を追いかけ、その先に自石がある」パターンを一括検出する。オセロのビットボード合法手生成は広く知られた手法であり、以下に具体的な実装を示す。

**シフト方向とマスクの対応表**

マスクは `opponent` に対して適用し、シフト先で列の回り込みが発生するビットを事前に除去する。各方向で「シフト操作の結果、隣接する行に回り込む可能性がある列」のみをマスクすればよい。

| 方向 | シフト量 | マスク | 回り込み防止の根拠 |
|------|---------|--------|-------------------|
| 右 (East) | +1 (左シフト) | `NotAFile` | 左シフトにより A列(bit0,8,16,...) の相手石が H列(bit7,15,23,...) に回り込むのを防止する。A列のビットを落とすことで回り込みを排除する。 |
| 左 (West) | -1 (右シフト) | `NotHFile` | 右シフトにより H列の相手石が A列に回り込むのを防止する。H列のビットを落とすことで回り込みを排除する。 |
| 下 (South) | +8 (左シフト) | `0xFFFFFFFFFFFFFFFF` | 行方向のシフトでは列の回り込みは発生しない。マスク不要（全ビット有効）。 |
| 上 (North) | -8 (右シフト) | `0xFFFFFFFFFFFFFFFF` | 同上。行方向のシフトでは列の回り込みは発生しない。 |
| 右下 (SE) | +9 (左シフト) | `NotAFile` | shift=9 は「1行下 + 1列右」に相当する。右方向の成分（+1）により A列→H列の回り込みが発生しうるため、`NotAFile` で防止する。下方向の成分（+8）は列の回り込みを起こさないため、`NotHFile` は不要である。 |
| 左上 (NW) | -9 (右シフト) | `NotHFile` | shift=-9 は「1行上 + 1列左」に相当する。左方向の成分（-1）により H列→A列の回り込みが発生しうるため、`NotHFile` で防止する。上方向の成分（-8）は列の回り込みを起こさない。 |
| 右上 (NE) | -7 (右シフト) | `NotAFile` | shift=-7 は「1行上 + 1列右」に相当する。右方向の成分（+1）により A列→H列の回り込みが発生しうるため、`NotAFile` で防止する。上方向の成分（-8）は列の回り込みを起こさない。 |
| 左下 (SW) | +7 (左シフト) | `NotHFile` | shift=7 は「1行下 + 1列左」に相当する。左方向の成分（-1）により H列→A列の回り込みが発生しうるため、`NotHFile` で防止する。下方向の成分（+8）は列の回り込みを起こさない。 |

対角線方向で片側の列マスクのみで十分な理由: 対角線シフトは「行方向 +/- 8」と「列方向 +/- 1」の合成である。行方向の成分は列の回り込みを起こさず、列方向の成分は片側の列の回り込みのみを起こす。したがって、列方向の成分が起こす回り込みを防止する片側マスクのみで十分であり、`NotAFile & NotHFile` の両端マスクは不要である。

`ComputeFlippedDirection`（5.1.3）でも同一のシフト量・マスク対応表を使用し、一貫性を保つ。

```csharp
/// <summary>
/// ビットボード演算による高速合法手生成機能を提供する静的クラスです。
/// 8 方向のシフト演算とマスク操作により、全合法手を一括で算出します。
/// </summary>
public static class BitboardMobilityGenerator
{
    // 列マスク: A列(左端)とH列(右端)のビットを落とすマスク
    // 左右方向と対角線方向のシフトで隣の行に回り込むことを防止する
    private const ulong NotAFile = 0xFEFEFEFEFEFEFEFEUL; // A列以外
    private const ulong NotHFile = 0x7F7F7F7F7F7F7F7FUL; // H列以外

    /// <summary>
    /// 指定された手番の全合法手をビットボードとして返します。
    /// 戻り値の各ビットが 1 であるマスが合法手です。
    /// </summary>
    /// <param name="player">手番側の石の配置</param>
    /// <param name="opponent">相手側の石の配置</param>
    /// <returns>合法手のビットボード</returns>
    public static ulong GenerateMoves(ulong player, ulong opponent)
    {
        ulong empty = ~(player | opponent);
        ulong moves = 0UL;

        // 8方向: 右, 左, 下, 上, 右下, 左上, 右上, 左下
        moves |= FindFlips(player, opponent, empty, 1, NotAFile);   // 右
        moves |= FindFlips(player, opponent, empty, -1, NotHFile);  // 左 (負シフト = 右シフト)
        moves |= FindFlips(player, opponent, empty, 8, 0xFFFFFFFFFFFFFFFFUL);  // 下
        moves |= FindFlips(player, opponent, empty, -8, 0xFFFFFFFFFFFFFFFFUL); // 上
        moves |= FindFlips(player, opponent, empty, 9, NotAFile);   // 右下
        moves |= FindFlips(player, opponent, empty, -9, NotHFile);  // 左上
        moves |= FindFlips(player, opponent, empty, 7, NotHFile);   // 左下
        moves |= FindFlips(player, opponent, empty, -7, NotAFile);  // 右上

        return moves;
    }

    /// <summary>
    /// 指定方向について、相手石の連続を越えた先の空マスを合法手として検出します。
    /// </summary>
    /// <param name="player">手番側の石の配置</param>
    /// <param name="opponent">マスク適用後の相手石</param>
    /// <param name="empty">空マス</param>
    /// <param name="shift">シフト量（正:左シフト, 負:右シフト）</param>
    /// <param name="mask">列の回り込み防止マスク</param>
    /// <returns>この方向での合法手ビットボード</returns>
    private static ulong FindFlips(ulong player, ulong opponent, ulong empty,
                                    int shift, ulong mask)
    {
        ulong maskedOpponent = opponent & mask;
        ulong flipped;

        if (shift > 0)
        {
            flipped = maskedOpponent & (player << shift);
            flipped |= maskedOpponent & (flipped << shift);
            flipped |= maskedOpponent & (flipped << shift);
            flipped |= maskedOpponent & (flipped << shift);
            flipped |= maskedOpponent & (flipped << shift);
            flipped |= maskedOpponent & (flipped << shift);
            return empty & (flipped << shift);
        }
        else
        {
            int rshift = -shift;
            flipped = maskedOpponent & (player >> rshift);
            flipped |= maskedOpponent & (flipped >> rshift);
            flipped |= maskedOpponent & (flipped >> rshift);
            flipped |= maskedOpponent & (flipped >> rshift);
            flipped |= maskedOpponent & (flipped >> rshift);
            flipped |= maskedOpponent & (flipped >> rshift);
            return empty & (flipped >> rshift);
        }
    }

    /// <summary>
    /// 合法手のビットボードから合法手の数を返します。
    /// </summary>
    /// <param name="moves">合法手のビットボード</param>
    /// <returns>合法手の数</returns>
    public static int CountMoves(ulong moves)
    {
        return BitOperations.PopCount(moves);
    }

    /// <summary>
    /// 合法手のビットボードから合法手のインデックスリストを生成します。
    /// </summary>
    /// <param name="moves">合法手のビットボード</param>
    /// <returns>合法手のインデックスリスト</returns>
    public static List<int> ToMoveList(ulong moves)
    {
        var list = new List<int>(BitOperations.PopCount(moves));
        while (moves != 0)
        {
            int index = BitOperations.TrailingZeroCount(moves);
            list.Add(index);
            moves &= moves - 1; // 最下位ビットをクリア
        }
        return list;
    }
}
```

#### 5.1.3 裏返し処理のビットボード化

`MoveAndReverseUpdater.Update` の裏返し処理（着手実行モード）もビットボード化する。着手位置と方向から裏返すべき石を一括計算する。`ComputeFlippedDirection` は 5.1.2 の `FindFlips` と同一のシフト量・マスク対応表を使用する。

```csharp
/// <summary>
/// 指定位置に着手した場合に裏返される石のビットボードを計算します。
/// </summary>
/// <param name="player">手番側の石の配置</param>
/// <param name="opponent">相手側の石の配置</param>
/// <param name="move">着手位置（0-63）</param>
/// <returns>裏返される石のビットボード</returns>
public static ulong ComputeFlipped(ulong player, ulong opponent, int move)
{
    ulong moveBit = 1UL << move;
    ulong flipped = 0UL;

    // 8方向それぞれについて裏返し判定（マスク対応は FindFlips と同一）
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, 1, NotAFile);   // 右
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, -1, NotHFile);  // 左
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, 8, 0xFFFFFFFFFFFFFFFFUL);  // 下
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, -8, 0xFFFFFFFFFFFFFFFFUL); // 上
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, 9, NotAFile);   // 右下
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, -9, NotHFile);  // 左上
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, 7, NotHFile);   // 左下
    flipped |= ComputeFlippedDirection(player, opponent, moveBit, -7, NotAFile);  // 右上

    return flipped;
}
```

#### 5.1.4 MobilityAnalyzer への統合

`MobilityAnalyzer` の `Analyze` / `AnalyzeCount` をビットボード版に置き換える。

```csharp
// MobilityAnalyzer.cs - 変更後
public List<int> Analyze(GameContext context, Disc.Color turn)
{
    var (player, opponent) = GetPlayerOpponent(context, turn);
    ulong moves = BitboardMobilityGenerator.GenerateMoves(player, opponent);
    context.Mobility = moves;
    return BitboardMobilityGenerator.ToMoveList(moves);
}

public int AnalyzeCount(GameContext context, Disc.Color turn)
{
    var (player, opponent) = GetPlayerOpponent(context, turn);
    ulong moves = BitboardMobilityGenerator.GenerateMoves(player, opponent);
    return BitboardMobilityGenerator.CountMoves(moves);
}

private static (ulong player, ulong opponent) GetPlayerOpponent(GameContext context, Disc.Color turn)
{
    if (turn == Disc.Color.Undefined) turn = context.Turn;
    return turn == Disc.Color.Black
        ? (context.Black, context.White)
        : (context.White, context.Black);
}
```

**[仮定]** ビットボード合法手生成の結果は、現行の `MoveAndReverseUpdater.Update` による逐次判定と完全に一致する前提である。実装後に全マスの合法手判定結果が一致することを網羅的にテストする。

**効果**: 合法手生成の実行時間を 1/10〜1/20 に削減する。ビットボード方式では条件分岐が大幅に削減され、CPU のパイプライン効率も向上する。

#### 5.1.5 PvsSearchEngine.MakeMove のビットボード化と MoveAndReverseUpdater の責務分離

現行の `PvsSearchEngine.MakeMove` は `_reverseUpdater.Update(context)` を呼び出して着手を実行している。ビットボード化後は、`BitboardMobilityGenerator.ComputeFlipped` + ビットボード演算で盤面を直接更新する。これにより `PvsSearchEngine` から `MoveAndReverseUpdater` への依存を排除する。

**MakeMove のビットボード化後のコード例:**

```csharp
private MoveInfo MakeMove(GameContext context, int move)
{
    // 着手前の状態を保存
    var info = new MoveInfo
    {
        PrevBlack = context.Black,
        PrevWhite = context.White,
        PrevTurn = context.Turn,
        PrevTurnCount = context.TurnCount,
        PrevStage = context.Stage,
        PrevMove = context.Move,
        PrevMobility = context.Mobility,
    };

    // ビットボード演算で裏返し石を計算
    var (player, opponent) = context.Turn == Disc.Color.Black
        ? (context.Black, context.White)
        : (context.White, context.Black);
    ulong flipped = BitboardMobilityGenerator.ComputeFlipped(player, opponent, move);

    // flipped を MoveInfo に保存（Zobrist 差分更新・評価関数差分更新で使用）
    info.Flipped = flipped;

    // ビットボード演算で盤面を更新
    ulong moveBit = 1UL << move;
    player |= moveBit | flipped;    // 着手位置 + 裏返し石を自石に追加
    opponent &= ~flipped;           // 裏返し石を相手石から除去

    // コンテキストに反映
    if (context.Turn == Disc.Color.Black)
    {
        context.Black = player;
        context.White = opponent;
    }
    else
    {
        context.White = player;
        context.Black = opponent;
    }

    context.Move = move;
    BoardAccessor.NextTurn(context);

    return info;
}
```

`UnmakeMove` は従来通り `MoveInfo` から全フィールドを復元するため、変更不要である。

**MoveAndReverseUpdater の位置づけ:**

- `MoveAndReverseUpdater` は探索エンジン（`PvsSearchEngine`）からは使用しなくなる。DI コンテナからの注入も不要となる。
- ただし、`MoveAndReverseUpdater` は UI 層（Windows Forms）での着手実行や、合法手判定（`Update(context, move)` の分析モード）で引き続き使用される。したがって、クラス自体は削除せず、探索エンジン外の用途で維持する。
- `PvsSearchEngine` のコンストラクタから `MoveAndReverseUpdater reverseUpdater` パラメータを削除し、`_reverseUpdater` フィールドも削除する。

**`flipped` ビットボードの伝播経路:**

`MakeMove` が返す `MoveInfo.Flipped` は以下の 2 箇所で活用される:
1. **Zobrist ハッシュ差分更新** (5.2): `_zobristHash.UpdateHash(currentHash, move, info.Flipped, turn)` で使用
2. **評価関数差分更新** (5.4): 変化マスの特定に使用（`moveBit | info.Flipped` が全変化マス）

### 5.2 Zobrist ハッシュの差分更新

**対象ファイル**: `Reluca/Search/Transposition/ZobristHash.cs`, `Reluca/Search/Transposition/IZobristHash.cs`, `Reluca/Search/PvsSearchEngine.cs`

#### 5.2.1 現状の問題

`ZobristHash.ComputeHash` は呼び出しのたびに 64 マス全てをループし、各マスの石の有無を確認してハッシュを計算している。`PvsSearchEngine.Pvs` 内では TT Probe のためにノードごとに呼ばれ、`RootSearch` 内でも TT Store と `OptimizeMoveOrder` で呼ばれる。

#### 5.2.2 差分更新の原理

Zobrist ハッシュの性質として、XOR は自己逆元である（A XOR A = 0）。したがって、着手による盤面変化は以下のように差分更新できる:

1. 着手位置に手番の石を配置: `hash ^= PieceKeys[move, turnIndex]`
2. 裏返された各マスについて:
   - 相手の石を除去: `hash ^= PieceKeys[square, opponentIndex]`
   - 手番の石を配置: `hash ^= PieceKeys[square, turnIndex]`
3. 手番の変更: `hash ^= TurnKey`

裏返された石の数を n とすると、XOR 操作は 1 + 2n + 1 = 2n + 2 回で済む（フルスキャンの 64 回に対して劇的に少ない）。

#### 5.2.3 インターフェース変更

```csharp
// IZobristHash.cs - 差分更新メソッドを追加
public interface IZobristHash
{
    /// <summary>
    /// 盤面全体からハッシュ値を計算します（初回計算用）。
    /// </summary>
    ulong ComputeHash(GameContext context);

    /// <summary>
    /// 着手による差分からハッシュ値を更新します。
    /// </summary>
    /// <param name="currentHash">現在のハッシュ値</param>
    /// <param name="move">着手位置（0-63）</param>
    /// <param name="flipped">裏返された石のビットボード</param>
    /// <param name="turn">着手した手番の色</param>
    /// <returns>更新後のハッシュ値</returns>
    ulong UpdateHash(ulong currentHash, int move, ulong flipped, Disc.Color turn);
}
```

#### 5.2.4 差分更新の実装

```csharp
// ZobristHash.cs - 差分更新メソッド
public ulong UpdateHash(ulong currentHash, int move, ulong flipped, Disc.Color turn)
{
    int turnIndex = turn == Disc.Color.Black ? ZobristKeys.BlackIndex : ZobristKeys.WhiteIndex;
    int opponentIndex = turn == Disc.Color.Black ? ZobristKeys.WhiteIndex : ZobristKeys.BlackIndex;

    // 着手位置に手番の石を配置
    currentHash ^= ZobristKeys.PieceKeys[move, turnIndex];

    // 裏返された石を更新（相手→自分）
    while (flipped != 0)
    {
        int square = BitOperations.TrailingZeroCount(flipped);
        currentHash ^= ZobristKeys.PieceKeys[square, opponentIndex]; // 相手石を除去
        currentHash ^= ZobristKeys.PieceKeys[square, turnIndex];     // 自石を配置
        flipped &= flipped - 1; // 最下位ビットをクリア
    }

    // 手番変更
    currentHash ^= ZobristKeys.TurnKey;

    return currentHash;
}
```

#### 5.2.5 PvsSearchEngine への統合 -- ハッシュ値伝播方式

ハッシュ値の伝播方式として **`Pvs` メソッドの引数方式** を採用する。`GameContext` にフィールドを追加する方式と比較して、以下の利点がある:

- `GameContext` への変更が不要であり、既存の全コードへの影響が限定的である
- ハッシュ値の管理が `PvsSearchEngine` 内で完結し、責務が明確である
- `MoveInfo` への `PrevHash` 保存と組み合わせることで、UnmakeMove 時の復元も自然に設計できる

**MoveInfo の拡張:**

```csharp
private struct MoveInfo
{
    // ... 既存フィールド ...
    public ulong PrevHash;      // 着手前のハッシュ値
    public ulong Flipped;       // 裏返された石のビットボード
}
```

**Pvs メソッドのシグネチャ変更:**

```csharp
// 変更前
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed)

// 変更後: currentHash パラメータを追加
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed, ulong currentHash)
```

**RootSearch でのハッシュ初期化と伝播:**

```csharp
private SearchResult RootSearch(GameContext context, int depth, long alpha, long beta)
{
    var moves = _mobilityAnalyzer.Analyze(context);
    if (moves.Count == 0)
    {
        return new SearchResult(-1, Evaluate(context));
    }

    // ルート局面でフルスキャンによるハッシュ計算（1回のみ）
    ulong rootHash = 0;
    if (_options?.UseTranspositionTable == true)
    {
        rootHash = _zobristHash.ComputeHash(context);
    }

    moves = OptimizeMoveOrder(moves, context, depth, rootHash);

    // ... alpha/beta 初期化 ...

    foreach (var move in moves)
    {
        var moveInfo = MakeMove(context, move);
        // MakeMove 後にハッシュを差分更新
        ulong childHash = 0;
        if (_options?.UseTranspositionTable == true)
        {
            childHash = _zobristHash.UpdateHash(rootHash, move, moveInfo.Flipped, moveInfo.PrevTurn);
        }

        long score;
        try
        {
            score = -Pvs(context, depth - 1, -beta, -alpha, false, childHash);
        }
        finally
        {
            UnmakeMove(context, moveInfo);
            // UnmakeMove 後はハッシュ自動復元不要（rootHash を保持し続ける）
        }
        // ...
    }

    // TT Store（rootHash を使用）
    if (_options?.UseTranspositionTable == true && !_suppressTTStore)
    {
        var boundType = DetermineBoundType(maxValue, originalAlpha, beta);
        _transpositionTable.Store(rootHash, depth, maxValue, boundType, rootBestMove);
    }

    return new SearchResult(rootBestMove, maxValue);
}
```

**Pvs メソッド内でのハッシュ管理:**

```csharp
private long Pvs(GameContext context, int remainingDepth, long alpha, long beta, bool isPassed, ulong currentHash)
{
    _nodesSearched++;

    // ... タイムアウトチェック、終了条件 ...

    // TT Probe（currentHash をそのまま使用、ComputeHash 呼び出し不要）
    if (_options?.UseTranspositionTable == true)
    {
        if (_transpositionTable.TryProbe(currentHash, remainingDepth, alpha, beta, out var entry))
        {
            return entry.Value;
        }
    }

    // MPC 判定
    if (_mpcEnabled && !isPassed)
    {
        // MPC の浅い探索にも currentHash を伝播する
        var mpcResult = TryMultiProbCut(context, remainingDepth, alpha, beta, currentHash);
        if (mpcResult.HasValue)
        {
            return mpcResult.Value;
        }
    }

    var moves = _mobilityAnalyzer.Analyze(context);

    if (moves.Count > 0)
    {
        // Move Ordering（TT bestMove の取得に currentHash を使用）
        // ...

        foreach (var move in moves)
        {
            var moveInfo = MakeMove(context, move);
            // MakeMove 後にハッシュを差分更新
            ulong childHash = 0;
            if (_options?.UseTranspositionTable == true)
            {
                childHash = _zobristHash.UpdateHash(currentHash, move, moveInfo.Flipped, moveInfo.PrevTurn);
            }

            long score;
            try
            {
                if (isFirstMove)
                {
                    // 最初の手: フルウィンドウで探索
                    score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false, childHash);
                    isFirstMove = false;
                }
                else
                {
                    // 2手目以降: Null Window Search
                    score = -Pvs(context, remainingDepth - 1, -alpha - 1, -alpha, false, childHash);
                    if (score > alpha && score < beta)
                    {
                        // fail-high: フルウィンドウで再探索
                        // childHash の再利用根拠:
                        // NWS 再探索は MakeMove を再実行しない。現行の PvsSearchEngine.Pvs の
                        // 実装では、MakeMove は foreach ループの先頭で 1 回のみ実行され（try ブロック外）、
                        // NWS → fail-high → フルウィンドウ再探索の一連のフローは同一の try ブロック内で
                        // 完結する。盤面状態は MakeMove 実行後から変化しないため、childHash は
                        // フルウィンドウ再探索でもそのまま有効である。
                        score = -Pvs(context, remainingDepth - 1, -beta, -alpha, false, childHash);
                    }
                }
            }
            finally
            {
                UnmakeMove(context, moveInfo);
                // UnmakeMove 後は currentHash が引き続き有効
            }
            // ...
        }
    }
    else
    {
        // パス処理: 盤面は変化しないため flipped = 0、着手位置もない
        // 手番変更のみなので hash ^= TurnKey で差分更新する
        ulong passHash = currentHash ^ ZobristKeys.TurnKey;
        var prevTurn = context.Turn;
        BoardAccessor.Pass(context);
        try
        {
            maxValue = -Pvs(context, remainingDepth - 1, -beta, -alpha, true, passHash);
        }
        finally
        {
            context.Turn = prevTurn;
            // 復元後は currentHash が引き続き有効
        }
    }

    // TT Store（currentHash を使用）
    if (_options?.UseTranspositionTable == true && !_suppressTTStore && moves.Count > 0)
    {
        var boundType = DetermineBoundType(maxValue, originalAlpha, beta);
        _transpositionTable.Store(currentHash, remainingDepth, maxValue, boundType, localBestMove);
    }

    return maxValue;
}
```

**MPC 浅い探索でのハッシュ伝播:**

```csharp
private long? TryMultiProbCut(GameContext context, int remainingDepth, long alpha, long beta, ulong currentHash)
{
    // ...
    // MPC の浅い探索は同一局面に対する別深さの探索であるため、
    // currentHash をそのまま渡す（盤面が変化しないため差分更新は不要）
    long shallowValue = Pvs(context, pair.ShallowDepth, DefaultAlpha, DefaultBeta, false, currentHash);
    // ...
}
```

**OptimizeMoveOrder でのハッシュ使用:**

```csharp
private List<int> OptimizeMoveOrder(List<int> moves, GameContext context, int depth, ulong currentHash)
{
    // TT bestMove の取得に currentHash を使用（ComputeHash 呼び出し不要）
    if (_options?.UseTranspositionTable == true)
    {
        int ttBestMove = _transpositionTable.GetBestMove(currentHash);
        // ...
    }
    // ...
}
```

**[仮定]** `MoveAndReverseUpdater.Update` の裏返し処理をビットボード化（5.1.3）した結果として `flipped` ビットボードが得られる前提である。5.1 のビットボード合法手生成と本項目は密結合しており、5.1 の実装が先行する必要がある。

**効果**: ノードあたりの Zobrist ハッシュ計算コストを O(64) から O(n)（n = 裏返し枚数、通常 2〜10）に削減する。加えて、`Pvs` 内の `ComputeHash` 呼び出し（毎ノード O(64)）が完全に排除される。

### 5.3 `BitOperations.PopCount()` の利用

**対象ファイル**: `Reluca/Accessors/BoardAccessor.cs`

#### 5.3.1 現状の問題

`BoardAccessor.GetDiscCount` は 64 回のループとビットマスク判定で石数をカウントしている。

```csharp
// 現状（BoardAccessor.cs:296-310）
public static int GetDiscCount(BoardContext context, Disc.Color color)
{
    var result = 0;
    var target = color == Disc.Color.Black ? context.Black : context.White;
    for (var i = 0; i < Board.AllLength; i++)
    {
        if ((target & (1ul << i)) > 0)
        {
            result++;
        }
    }
    return result;
}
```

#### 5.3.2 PopCount による置換

```csharp
using System.Numerics;

// 変更後
public static int GetDiscCount(BoardContext context, Disc.Color color)
{
    var target = color == Disc.Color.Black ? context.Black : context.White;
    return BitOperations.PopCount(target);
}
```

`BitOperations.PopCount(ulong)` は .NET 8.0 では JIT が `POPCNT` CPU 命令に直接コンパイルする（SSE4.2 / BMI1 対応 CPU が必要、2008 年以降の Intel/AMD CPU はほぼ全て対応）。`POPCNT` 非対応環境ではソフトウェアフォールバック（テーブルルックアップ）が使用されるが、それでも 64 回ループよりは高速である。

**[仮定]** 実行環境が SSE4.2 / BMI1 対応 CPU であることを前提とする。.NET 8.0 のランタイムは非対応環境でも正しく動作する（ソフトウェアフォールバック）ため、機能的な互換性は保たれる。

#### 5.3.3 追加のビットカウント活用箇所

`PopCount` の導入に伴い、以下の箇所でも活用が可能である:

- **`BitboardMobilityGenerator.CountMoves`**: 合法手数のカウント（5.1 で既に使用）
- **`BitboardMobilityGenerator.ToMoveList`**: `TrailingZeroCount` によるビットスキャン（5.1 で既に使用）
- **空マス数の算出**: `64 - PopCount(black | white)` で O(1) で計算可能

**効果**: `GetDiscCount` の実行時間を 64 回ループ → 1 CPU 命令に削減する。終盤完全読み切りフェーズでは石数カウントが全ノードで呼ばれるため、効果は特に大きい。

### 5.4 評価関数の差分更新

**対象ファイル**: `Reluca/Evaluates/FeaturePatternExtractor.cs`, `Reluca/Evaluates/FeaturePatternEvaluator.cs`, `Reluca/Search/PvsSearchEngine.cs`

#### 5.4.1 現状の問題

`FeaturePatternExtractor.ExtractNoAlloc` は毎回 13 種類のパターンの全マスを走査して 3 進数インデックスを計算している。パターンのマス数の合計は以下の通りである:

| パターン | マス数 | サブパターン数 | 合計マスアクセス |
|---------|--------|---------------|----------------|
| Diag4 | 4 | 4 | 16 |
| Diag5 | 5 | 4 | 20 |
| Diag6 | 6 | 4 | 24 |
| Diag7 | 7 | 4 | 28 |
| Diag8 | 8 | 2 | 16 |
| HorVert2 | 8 | 4 | 32 |
| HorVert3 | 8 | 4 | 32 |
| HorVert4 | 8 | 4 | 32 |
| Edge2X | 10 | 4 | 40 |
| Corner2X5 | 10 | 4 | 40 |
| Corner3X3 | 9 | 4 | 36 |
| **合計** | | | **316** |

1 ノードあたり 316 回のビットマスク判定 + 3 進数乗算が発生している。

#### 5.4.2 差分更新の原理

着手で変化するマスは以下の通りである:
- 着手位置 1 マス（空 → 手番色）
- 裏返されたマス n 個（相手色 → 手番色）

合計 n + 1 マスの変化に対して、「そのマスを含むパターン」のインデックスのみを更新すれば良い。パターンの 3 進数インデックスにおいて、1 マスの変化は以下のように差分計算できる:

- マスの 3 進数における重み（桁）を `w` とする（3^i、i はパターン内のマス位置）
- 空 → 黒: `index += 2 * w - 1 * w = w`（Empty=1 → Black=2）
- 空 → 白: `index += 0 * w - 1 * w = -w`（Empty=1 → White=0）
- 白 → 黒: `index += 2 * w - 0 * w = 2w`（White=0 → Black=2）
- 黒 → 白: `index += 0 * w - 2 * w = -2w`（Black=2 → White=0）

**[仮定]** 現行の `ConvertToTernaryIndex` は MSB 側（パターン定義の先頭マス）から `index *= 3; index += value` で計算しているため、パターン定義の i 番目のマス（0-indexed）に対応する重みは `3^(length - 1 - i)` となる。逆引きテーブルの `TernaryWeight` はこの計算方向に合わせて設定する。実装時にパターン定義のマス順序と重み計算の整合性を網羅的に検証する。

#### 5.4.3 マス → パターン逆引きテーブル

差分更新には「あるマスが変化した時、どのパターンに影響するか」を高速に逆引きする必要がある。初期化時に以下の逆引きテーブルを構築する。

```csharp
/// <summary>
/// マスからパターンへの逆引き情報です。
/// あるマスが変化した時に影響を受けるパターンとその差分計算に必要な情報を保持します。
/// </summary>
public readonly struct PatternMapping
{
    /// <summary>
    /// パターンの種類
    /// </summary>
    public readonly FeaturePattern.Type PatternType;

    /// <summary>
    /// パターン内のサブパターンインデックス
    /// </summary>
    public readonly int SubPatternIndex;

    /// <summary>
    /// このマスの 3 進数における重み（3^i）
    /// </summary>
    public readonly int TernaryWeight;
}

// 逆引きテーブル: SquareToPatterns[square] = PatternMapping[]
// 各マスに対して、そのマスを含む全パターンの情報を保持
private PatternMapping[][] _squareToPatterns; // [64][]
```

**逆引きテーブルと `Initialize` メソッドの整合性:**

`FeaturePatternExtractor.Initialize` メソッドが呼ばれた場合、`PatternPositions` が再設定されるため、逆引きテーブルも再構築が必要である。`Initialize` メソッド内で `BuildSquareToPatterns()` を呼び出し、逆引きテーブルを `PatternPositions` と同期させる。

```csharp
public void Initialize(Dictionary<string, List<List<ulong>>>? resource)
{
    var positions = resource.ToDictionary(r => FeaturePattern.GetType(r.Key), r => r.Value);
    PatternPositions = positions;

    // _preallocatedResults を PatternPositions に基づいて再構築する
    _preallocatedResults.Clear();
    foreach (var pattern in PatternPositions)
    {
        _preallocatedResults[pattern.Key] = new int[pattern.Value.Count];
    }

    // 逆引きテーブルも再構築する
    BuildSquareToPatterns();
}
```

#### 5.4.4 差分更新の統合とパターンインデックスの保存・復元方式

MakeMove/UnmakeMove パターンと統合し、パターンインデックスの保存・復元を行う。

**保存・復元方式の選定:**

パターンインデックスの保存・復元には、`_preallocatedResults` の差分のみを `MoveInfo` に記録する方式を採用する。具体的には、差分更新で変更されたサブパターンの「変更前のインデックス値」と「変更位置（PatternType + SubPatternIndex）」のペアを固定サイズバッファに記録する。

```csharp
/// <summary>
/// パターンインデックスの差分変更記録です。
/// UnmakeMove 時に _preallocatedResults を復元するために使用します。
/// </summary>
private struct PatternIndexChange
{
    /// <summary>
    /// パターンの種類
    /// </summary>
    public FeaturePattern.Type PatternType;

    /// <summary>
    /// サブパターンインデックス
    /// </summary>
    public int SubPatternIndex;

    /// <summary>
    /// 変更前のインデックス値
    /// </summary>
    public int PrevIndex;
}

// MoveInfo に追加
private struct MoveInfo
{
    // ... 既存フィールド ...
    public ulong PrevHash;      // 着手前のハッシュ値
    public ulong Flipped;       // 裏返された石のビットボード

    // パターンインデックスの差分変更記録
    // 1マスの変化で影響するパターンは平均 5〜8 個、変化マスは最大 11 個（着手1 + 裏返し最大10）程度
    // 最悪ケースを考慮して 128 個のバッファを確保する
    public PatternIndexChange[] PatternChanges;
    public int PatternChangeCount;
}
```

**MakeMove 時の差分更新フロー:**

```csharp
// MakeMove 内で差分更新を実行
info.PatternChanges = _patternChangeBuffer; // 事前確保済みバッファを共有
info.PatternChangeCount = 0;

// 着手位置の差分更新
UpdatePatternIndicesForSquare(move, /* empty -> turn */, ref info);

// 裏返し位置の差分更新
ulong tmpFlipped = flipped;
while (tmpFlipped != 0)
{
    int sq = BitOperations.TrailingZeroCount(tmpFlipped);
    UpdatePatternIndicesForSquare(sq, /* opponent -> turn */, ref info);
    tmpFlipped &= tmpFlipped - 1;
}
```

**UnmakeMove 時の復元フロー:**

```csharp
// UnmakeMove 内でパターンインデックスを復元
for (int i = info.PatternChangeCount - 1; i >= 0; i--)
{
    var change = info.PatternChanges[i];
    _preallocatedResults[change.PatternType][change.SubPatternIndex] = change.PrevIndex;
}
```

`PatternIndexChange[]` バッファは `PvsSearchEngine` のフィールドとして 1 つ事前確保し、`MoveInfo` からは参照で共有する。探索はシングルスレッドであり、再帰呼び出しの各レベルで MakeMove → Pvs → UnmakeMove が必ず順序通りに実行されるため、バッファの競合は発生しない。ただし、再帰のネスト（最大探索深さ分）を考慮して、各 MoveInfo が変更記録の開始オフセットを保持する方式とする。具体的には、単一の大きな配列（`PatternIndexChange[MaxDepth * MaxChangesPerMove]`）を確保し、`MoveInfo` には開始オフセットと件数を保持する。

**バッファサイズの具体値:**

| 定数 | 値 | 根拠 |
|------|-----|------|
| `MaxDepth` | 64 | オセロの最大手数は 60 手（初期 4 石 + 60 手 = 64 マス）であるが、パスを含む探索パスを考慮して 64 とする。現行の `PvsSearchEngine` の反復深化でも `options.MaxDepth` は 64 以下を想定している。 |
| `MaxChangesPerMove` | 128 | 1 手あたりの変化マス数は最大 11（着手 1 + 裏返し最大 10）であり、各マスが影響するパターン数は最大 8 個程度である。したがって 1 手あたりの最大パターン変更数は 11 × 8 = 88 であるが、安全マージンとして 2 のべき乗で切り上げた 128 とする。 |
| 合計バッファサイズ | 8,192 エントリ | `64 * 128 = 8,192`。`PatternIndexChange` は `FeaturePattern.Type`（enum: 4 bytes）+ `int SubPatternIndex`（4 bytes）+ `int PrevIndex`（4 bytes）= 12 bytes/エントリであり、合計 8,192 × 12 = 約 96 KB である。探索エンジンのインスタンスあたり 1 回のみ確保するため、メモリ使用量は無視できる水準である。 |

バッファオーバーフロー防止のため、`MakeMove` 内で開始オフセット + 変更件数が合計バッファサイズを超えないことをデバッグアサーションで検証する。

**[仮定]** 差分更新の正しさは「差分更新後のインデックス = フルスキャンによるインデックス」で検証できる。デバッグビルドでは差分更新後にフルスキャンの結果と照合するアサーションを挿入し、開発中のバグを早期に検出する。

**[仮定]** パターンの逆引きテーブルのサイズは 64 マス × 平均 5〜8 パターン/マス程度であり、メモリ使用量は無視できる水準である。

#### 5.4.5 実装上の複雑性と段階的導入

評価関数の差分更新は 4 項目中最も実装の複雑性が高い。以下の理由から、段階的な導入を推奨する:

1. **逆引きテーブルの正確な構築**: 既存のパターン定義（`feature_pattern` リソース）からマス位置を解析し、各マスがどのパターンのどの桁に対応するかを正確にマッピングする必要がある
2. **MakeMove/UnmakeMove との統合**: パターンインデックスの差分更新と復元を、既存の MakeMove/UnmakeMove パターンのフェイルセーフ設計（try/finally）と整合させる必要がある
3. **検証の難しさ**: 3 進数の差分計算は符号やオーバーフローに注意が必要であり、全パターンの全マスの組み合わせでの正しさを保証する必要がある

段階的導入:
- **Step 1**: `PopCount` と `BitboardMobilityGenerator` を先行導入（独立して検証可能）
- **Step 2**: Zobrist ハッシュの差分更新を導入（`flipped` ビットボードが必要なため Step 1 に依存）
- **Step 3**: 評価関数の差分更新を導入（最も複雑であり、Step 1/2 が安定した後に着手）

**効果**: ノードあたりのパターンインデックス計算コストを O(316) から O((n+1) × 平均影響パターン数) に削減する。n = 裏返し枚数（通常 2〜10）、影響パターン数はマスにより 3〜8 個程度であり、典型的には O(30〜80) 程度となる。フルスキャンの O(316) に対して 1/4〜1/10 の計算量となる。

## 6. 代替案の検討 (Alternatives Considered)

### 案A: ビットボード合法手生成のみ先行導入（部分導入）

- **概要**: 4 項目のうち、ビットボード合法手生成と `PopCount` のみを先行導入し、差分更新（Zobrist ハッシュ、評価関数）は保留する。
- **長所**: 変更範囲が限定的であり、差分更新の複雑な実装を避けられる。ビットボード合法手生成だけでも合法手判定の計算量は劇的に削減される。`PopCount` の導入は 1 行の変更で済む。
- **短所**: Zobrist ハッシュの O(64) フルスキャンと評価関数の O(316) フルスキャンが残る。これらは Phase 1/2 で解消されないボトルネックであり、探索深度が増すほど影響が大きくなる。

### 案B: 4 項目を一括導入（本提案）

- **概要**: ビットボード合法手生成、Zobrist ハッシュ差分更新、`PopCount`、評価関数差分更新の 4 項目を段階的に導入する。
- **長所**: 探索エンジンの全ホットパスにわたって最適化が行われ、根本的な高速化が実現される。ビットボード合法手生成で得られる `flipped` ビットボードを Zobrist ハッシュ差分更新と評価関数差分更新の両方で活用でき、相乗効果がある。
- **短所**: 評価関数の差分更新の実装が複雑であり、逆引きテーブルの構築と 3 進数差分計算のバグリスクが高い。全項目の実装・検証に時間を要する。

### 案C: Magic Bitboard による合法手生成

- **概要**: 本提案のシフト演算ベースの合法手生成の代わりに、Magic Bitboard（ルックアップテーブル＋マジックナンバーによるインデックス変換）を使用する。
- **長所**: テーブルルックアップにより、各方向の合法手生成が 1 回のメモリアクセスで完了する。チェスエンジンで広く使われている手法である。
- **短所**: オセロではチェスと異なり「裏返し」の処理が必要であり、Magic Bitboard の恩恵がチェスほど大きくない。テーブルのメモリ消費（数 KB〜数十 KB）が発生する。マジックナンバーの事前計算が必要であり、実装の複雑性が高い。シフト演算ベースのアプローチでもオセロの合法手生成は十分に高速であり、追加の複雑性に見合う効果は限定的である。

### 選定理由

案 B を採用する。理由は以下の通りである:

1. ビットボード合法手生成で得られる `flipped` ビットボードは、Zobrist ハッシュ差分更新と評価関数差分更新の両方で活用できる。この 3 つの最適化は密結合しており、一括導入による相乗効果が大きい。
2. 段階的な導入計画（Step 1: ビットボード + PopCount → Step 2: Zobrist 差分更新 → Step 3: 評価関数差分更新）により、各ステップで独立した検証が可能であり、リスクを管理できる。
3. `PopCount` の導入は 1 行の変更で済み、リスクは皆無である。ビットボード合法手生成もオセロ AI では広く知られた標準的手法であり、参考実装が豊富に存在する。評価関数差分更新のみが高リスクであるが、Step 3 として最後に着手することでリスクを最小化する。
4. 案 C（Magic Bitboard）は、オセロの裏返し処理との相性を考慮するとシフト演算ベースのアプローチに対する優位性が限定的であり、実装の複雑性に見合わない。

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

- 本改善はパフォーマンス最適化であり、セキュリティ・プライバシーへの影響はない。

### 7.2 スケーラビリティとパフォーマンス

- **合法手生成の高速化**: 64 マス × 8 方向の逐次ループ → 8 方向のビットシフト演算により、合法手生成の実行時間を 1/10〜1/20 に削減する。
- **Zobrist ハッシュの差分更新**: O(64) → O(n+2) に削減。典型的な着手では n=2〜10 であり、5〜30 倍の高速化を見込む。
- **PopCount**: 64 回ループ → 1 CPU 命令に削減。定数時間（`POPCNT` の Latency は 3 サイクル程度）。
- **評価関数の差分更新**: O(316) → O(30〜80) に削減。3〜10 倍の高速化を見込む。
- **総合効果**: Phase 1/2 で GC 圧力を排除した上で、Phase 3 でアルゴリズムレベルの計算量を削減することにより、同一時間でより深い探索が可能になる。目標として、同一局面・同一深さでの探索速度を Phase 2 完了時点から 2〜5 倍に改善する。

**ベンチマーク条件:**

性能改善の効果を定量的に検証するため、以下のベンチマーク条件を定義する。各ステップ完了後にこの条件で計測を実施する。

| 項目 | 条件 |
|------|------|
| 計測局面 | (1) 初期局面（開始直後）、(2) 中盤局面（20手目前後、多数の合法手がある局面）、(3) 終盤局面（50手目前後、空マス14以下） |
| 探索深さ | 各局面について depth=8, depth=10 の 2 パターン |
| 計測回数 | 各条件につき 5 回計測し、中央値を採用する |
| ウォームアップ | 計測前に同一条件で 2 回の探索を実行し、JIT コンパイルとキャッシュを安定させる |
| NPS 計算式 | `NPS = TotalNodes / ElapsedSeconds` （`TotalNodes` は反復深化の全深さの合計ノード数、`ElapsedSeconds` は探索開始から完了までの実時間） |
| 比較基準 | Phase 2 完了時点（PR #59 マージ直後）のコードベースで同一条件で計測した NPS をベースラインとする |

### 7.3 可観測性 (Observability)

- 既存のログ出力（`_logger.LogInformation` による探索進捗ログ: Depth, Nodes, TotalNodes, Value, MpcCuts, DepthElapsedMs 等）は維持する。
- NPS（Nodes Per Second）メトリクスを探索完了ログに追加し、Phase 3 前後の性能比較を容易にする。

**デバッグビルド限定の計測カウンタ:**

Phase 3 の各最適化の個別効果を定量的に評価するため、デバッグビルド（`#if DEBUG`）で以下のカウンタを追加する:

| カウンタ | 計測対象 | 目的 |
|---------|---------|------|
| `BitboardMoveGenCount` | `BitboardMobilityGenerator.GenerateMoves` の呼び出し回数 | ビットボード合法手生成の利用頻度の確認 |
| `ZobristUpdateCount` | `UpdateHash` の呼び出し回数 | 差分更新の利用回数 |
| `ZobristFullScanCount` | `ComputeHash` の呼び出し回数 | フルスキャンの残存回数（理想は RootSearch の 1 回のみ） |
| `PatternDeltaUpdateCount` | パターンインデックス差分更新の呼び出し回数 | 差分更新の利用回数 |
| `PatternDeltaAvgAffected` | 差分更新あたりの平均影響パターン数 | 差分更新の実効コストの把握 |

探索完了時にログ出力し、各最適化の寄与度を定量的に評価できるようにする。

### 7.4 マイグレーションと後方互換性

- **API 互換性**: `ISearchEngine.Search` のインターフェースは変更しない。外部からの呼び出し方法に変更はない。
- **`IZobristHash` インターフェース変更**: `UpdateHash` メソッドを追加する。既存の `ComputeHash` はそのまま残す。テストや初回ハッシュ計算で使用するため、削除しない。
- **`MobilityAnalyzer` の内部変更**: `Analyze` / `AnalyzeCount` の公開インターフェースは変更しない。内部実装のみビットボード版に置換する。
- **`BoardAccessor.GetDiscCount` の内部変更**: 公開インターフェースは変更しない。戻り値が同一であることをテストで保証する。
- **`FeaturePatternExtractor` の変更**: `ExtractNoAlloc` の戻り値型は変更しない。差分更新用の新メソッドを追加する形とし、既存のフルスキャン版もデバッグ検証用に残す。
- **`PvsSearchEngine` の変更**: `MoveAndReverseUpdater` への依存を削除し、コンストラクタのシグネチャが変更される。DI コンテナの登録を更新する必要がある。
- **探索結果の一致**: 全改善項目はアルゴリズムの変更ではなく計算の効率化であるため、同一局面・同一深さでの探索結果（最善手・評価値）は変化しない。

## 8. テスト戦略 (Test Strategy)

### ビットボード合法手生成のテスト

- **網羅テスト**: 複数の局面（初期局面、中盤局面、終盤局面、コーナー局面）について、既存の `MoveAndReverseUpdater` ベースの合法手生成結果と `BitboardMobilityGenerator.GenerateMoves` の結果が完全一致することを検証する
- **裏返しテスト**: 同様に、`ComputeFlipped` の結果が既存の裏返し処理と一致することを全方向・全パターンで検証する
- **エッジケース**: 合法手が 0 個の局面（パス局面）、合法手が 1 個の局面、最大合法手数に近い局面を含める

### Zobrist ハッシュ差分更新のテスト

- **フルスキャン一致テスト**: 差分更新で計算したハッシュ値と、同一局面に対する `ComputeHash`（フルスキャン）の結果が一致することを検証する
- **MakeMove/UnmakeMove 一致テスト**: MakeMove → UpdateHash → UnmakeMove → 元のハッシュ値に戻ることを検証する
- **パス処理テスト**: パス時の `hash ^= TurnKey` による差分更新と、パス後の局面に対する `ComputeHash`（フルスキャン）の結果が一致することを検証する
- **複数手シーケンステスト**: 10 手以上の着手・復元シーケンスで一貫性を検証する

### PopCount のテスト

- **既存テスト**: `BoardAccessorTest` の `GetDiscCount` テストが通ることを確認する
- **境界値テスト**: 0 個、1 個、32 個、64 個の石がある局面でカウントが正しいことを検証する

### 評価関数差分更新のテスト

- **フルスキャン一致テスト**: 差分更新後の全パターンインデックスが、`ExtractNoAlloc`（フルスキャン）の結果と一致することを検証する
- **デバッグアサーション**: デバッグビルドでは差分更新のたびにフルスキャン結果との照合アサーションを実行する
- **複数手シーケンステスト**: 実際の対局棋譜を再現し、全手の評価値が差分更新版とフルスキャン版で一致することを検証する

### 回帰テスト

- 既存の全単体テスト（`Reluca.Tests`）が通ることを確認する
- 特定局面に対して Phase 3 適用前後で探索結果（最善手・評価値）が同一であることを検証する
- Windows Forms UI での実対局を実施し、体感速度の改善を確認する

## 9. 実装・リリース計画 (Implementation Plan)

### Step 1: ビットボード合法手生成 + PopCount（独立して検証可能）

| ステップ | 内容 | 成果物 |
|---------|------|--------|
| 1-1 | `BitboardMobilityGenerator` 静的クラスの新規作成 | 合法手生成・裏返し計算のビットボード実装 |
| 1-2 | 網羅テストの実装 | 既存の逐次判定との結果一致テスト |
| 1-3 | `MobilityAnalyzer` の内部実装をビットボード版に置換 | `Analyze` / `AnalyzeCount` のビットボード化 |
| 1-4 | `PvsSearchEngine.MakeMove` の裏返し処理をビットボード化し、`MoveAndReverseUpdater` 依存を削除 | `ComputeFlipped` + ビットボード演算による着手実行 |
| 1-5 | `BoardAccessor.GetDiscCount` を `BitOperations.PopCount()` に置換 | 1 行の変更 |
| 1-6 | 既存テスト全通過 + 性能計測 | ベンチマーク条件（7.2）に基づく NPS 比較レポート |

### Step 2: Zobrist ハッシュ差分更新（Step 1 の `flipped` に依存）

| ステップ | 内容 | 成果物 |
|---------|------|--------|
| 2-1 | `IZobristHash` に `UpdateHash` メソッドを追加 | インターフェース拡張 |
| 2-2 | `ZobristHash.UpdateHash` の実装 | 差分更新ロジック |
| 2-3 | `PvsSearchEngine` の `Pvs` に `currentHash` パラメータを追加し、差分更新に切り替え | ハッシュ値の伝播・保存・復元（パス処理含む） |
| 2-4 | フルスキャン一致テスト + パス処理テスト | 差分更新の正しさ検証 |
| 2-5 | 既存テスト全通過 + 性能計測 | ベンチマーク条件（7.2）に基づく NPS 比較レポート |

### Step 3: 評価関数差分更新（Step 1 の `flipped` に依存、最も複雑）

| ステップ | 内容 | 成果物 |
|---------|------|--------|
| 3-1 | マス → パターン逆引きテーブルの構築（`Initialize` 時の再構築含む） | `PatternMapping[][]` の初期化ロジック |
| 3-2 | 差分更新ロジックの実装 | `UpdatePatternIndices` メソッド |
| 3-3 | MakeMove/UnmakeMove との統合（`PatternIndexChange` バッファによる保存・復元） | パターンインデックスの保存・復元 |
| 3-4 | フルスキャン一致テスト + デバッグアサーション | 差分更新の正しさ検証 |
| 3-5 | 既存テスト全通過 + 性能計測 | ベンチマーク条件（7.2）に基づく NPS 比較レポート |

### リスク軽減策

- **段階的導入**: Step 1 → Step 2 → Step 3 の順で実装し、各ステップで独立した検証を行う。Step 1 のみでも十分な高速化効果が得られるため、Step 2/3 で問題が発生した場合は Step 1 の成果のみでリリースできる。
- **デバッグアサーション**: Step 2/3 の差分更新では、デバッグビルドでフルスキャン結果との照合アサーションを挿入し、バグの早期検出を図る。
- **フォールバック**: 差分更新に問題が発見された場合、既存のフルスキャン版（`ComputeHash`, `ExtractNoAlloc`）にフォールバックできる構造を維持する。
- **各ステップ完了ごとに既存テストの通過を確認する**: 回帰テストの通過を必須条件とし、テストが通らない状態ではマージしない。

### システム概要ドキュメントへの影響

- **`docs/architecture.md`**: Phase 3 完了後に以下を更新する必要がある:
  - `Analyzers` セクションに `BitboardMobilityGenerator` の記述を追加
  - `Transposition` セクションに Zobrist ハッシュ差分更新の記述を追加
  - 技術スタックに `System.Numerics.BitOperations` の活用を記載
- **`docs/domain-model.md`**: ビットボード合法手生成の導入に伴い、「盤面表現（ビットボード）」セクションにビット演算による合法手生成の概要を追記する
- **`docs/api-overview.md`**: 存在しないため影響なし
