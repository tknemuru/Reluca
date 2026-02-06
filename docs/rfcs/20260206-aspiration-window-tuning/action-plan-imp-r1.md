# レビュー統合アクションプラン（ラウンド 1）

## 判定
- **Status**: Request Changes
- **P0 件数**: 0 件
- **P1 件数**: 3 件（アクション対象: 3 件）
- **P2 件数**: 3 件（記録のみ）

## 必須対応 (P0)

該当なし

## 推奨対応 (P1) — 上位5件

**[P1-1] 固定 2 倍拡張パスにおけるオーバーフロー保護の欠如**
- **出典**: Security & Risk Reviewer
- **内容**: `ExpandDelta` の固定 2 倍拡張パス（`useExponentialExpansion = false`）では `delta * 2` を直接計算した後に `MaxDelta` でクランプしているが、`delta * 2` 自体が `long.MaxValue` を超える場合にオーバーフローが発生する。指数拡張パスでは `ClampDelta` でオーバーフロー防止を行っているが、固定 2 倍パスでは同等の保護がない。
- **修正方針**: 固定 2 倍拡張パスでも `ClampDelta(delta, 2)` を使用し、指数拡張パスと保護レベルを統一する。

**[P1-2] InternalsVisibleTo の追加が RFC の変更対象ファイル一覧に未記載**
- **出典**: Approach Reviewer
- **内容**: `Reluca.csproj` に `InternalsVisibleTo Include="Reluca.Tests"` が追加されているが、RFC の変更対象ファイル一覧（Section 5.9）に `Reluca.csproj` は含まれていない。テスト容易性のための妥当な変更であるが、RFC との乖離が存在する。
- **修正方針**: RFC の変更対象ファイル一覧に `Reluca/Reluca.csproj` を追記する。実装自体の変更は不要である。

**[P1-3] AspirationParameterTable.cs の using ディレクティブ配置がコードベースと不統一**
- **出典**: Technical Quality Reviewer
- **内容**: `AspirationParameterTable.cs` では `using Reluca.Models;` が ModuleDoc の前に配置されているが、`PvsSearchEngine.cs` では `using` が ModuleDoc の後に配置されている。コードベース内のフォーマットが不統一である。
- **修正方針**: `using Reluca.Models;` を ModuleDoc の `/// <summary>` ブロックの後、`namespace` ブロックの前に移動し、`PvsSearchEngine.cs` のフォーマットと統一する。

## 記録のみ (P2)

- ExpandDelta / ClampDelta のアクセス修飾子が RFC では `private static` であるが実装では `internal static` に変更されている。テスト可能性のための合理的な変更である。（出典: Approach Reviewer）
- `ClampDelta` の `Debug.Assert(factor > 0, ...)` はリリースビルドでは無効化される。現在の呼び出し元では `factor` は常に正であるため実害はないが、防御的プログラミングの観点から記録する。（出典: Security & Risk Reviewer）
- `PvsSearchEngineAspirationTuningUnitTest.cs` でファイル全体に `#pragma warning disable CS8602` が適用されている。既存テストファイルとの一貫性は保たれているが、将来的には null チェック追加が望ましい。（出典: Technical Quality Reviewer）

## 要判断（矛盾する指摘）

該当なし
