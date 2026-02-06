# Architecture

## 概要

Reluca は、オセロ（リバーシ）の思考エンジンを提供する C# ライブラリである。PVS（Principal Variation Search / NegaScout）アルゴリズムによる探索、特徴パターンベースの局面評価、置換表（Transposition Table）によるキャッシングを実装し、強力な着手決定を実現する。

## ディレクトリ構成

```
Reluca/
├── Reluca/                 # メインライブラリ（思考エンジンコア）
│   ├── Search/             # 探索エンジン（PVS）
│   │   └── Transposition/  # 置換表（Zobrist ハッシング）
│   ├── Evaluates/          # 局面評価（特徴パターン評価）
│   ├── Movers/             # 指し手決定
│   ├── Updaters/           # ゲーム状態更新
│   ├── Analyzers/          # 分析（着手可能数など）
│   ├── Accessors/          # 盤面アクセサ
│   ├── Contexts/           # コンテキスト（GameContext, BoardContext）
│   ├── Models/             # ドメインモデル
│   ├── Converters/         # 型変換
│   ├── Di/                 # 依存性注入
│   ├── Services/           # サービス
│   └── Resources/          # 評価値テーブル（埋め込みリソース）
├── Reluca.Tests/           # 単体テスト
├── Reluca.Ui.WinForms/     # UI 層（Windows Forms）
├── Reluca.Tools/           # ツール（学習データ生成など）
├── Reluca.Tools.Tests/     # ツールのテスト
└── docs/                   # ドキュメント
```

## レイヤー構造

```
┌─────────────────────────────────────────────────┐
│  UI 層（Reluca.Ui.WinForms）                    │
│  - Windows Forms による対局インターフェース     │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│  ビジネスロジック層（Reluca）                   │
│  ┌───────────────────────────────────────────┐  │
│  │ Movers: 指し手決定                        │  │
│  │  - FindBestMover（最善手探索）            │  │
│  │  - FindFirstMover（最初の合法手）         │  │
│  └───────────────────────────────────────────┘  │
│                      │                          │
│                      ▼                          │
│  ┌───────────────────────────────────────────┐  │
│  │ Search: 探索エンジン                      │  │
│  │  - PvsSearchEngine（PVS/NegaScout）       │  │
│  └───────────────────────────────────────────┘  │
│           │                     │               │
│           ▼                     ▼               │
│  ┌─────────────────┐  ┌─────────────────────┐   │
│  │ Evaluates       │  │ Transposition       │   │
│  │ - 特徴パターン  │  │ - 置換表            │   │
│  │   評価関数      │  │ - Zobrist ハッシュ  │   │
│  └─────────────────┘  └─────────────────────┘   │
│           │                                     │
│           ▼                                     │
│  ┌───────────────────────────────────────────┐  │
│  │ Updaters / Accessors / Analyzers          │  │
│  │  - 盤面操作・状態更新・分析               │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│  データ層                                       │
│  - Contexts: ゲーム状態（record 型）            │
│  - Models: ドメインモデル                       │
│  - Resources: 評価値テーブル（埋め込みリソース）│
└─────────────────────────────────────────────────┘
```

## 主要コンポーネント

### Search（探索エンジン）

| クラス | 責務 |
|--------|------|
| `PvsSearchEngine` | PVS（Principal Variation Search / NegaScout）による探索。反復深化、Aspiration Window、置換表、Multi-ProbCut を統合 |
| `MpcParameterTable` | Multi-ProbCut のステージ別・カットペア別回帰パラメータテーブル |
| `MpcParameters` | Multi-ProbCut の回帰パラメータ（a, b, sigma）を保持するデータクラス |
| `MpcCutPair` | Multi-ProbCut のカットペア定義（浅い探索深さ、深い探索深さ）を保持するデータクラス |
| `AspirationParameterTable` | Aspiration Window のステージ別 delta テーブル。序盤/中盤/終盤で異なる初期 delta を管理し、深さ補正を適用する |

### Transposition（置換表）

| クラス | 責務 |
|--------|------|
| `ZobristTranspositionTable` | Zobrist ハッシングによる置換表実装 |
| `ZobristHash` | 局面の Zobrist ハッシュ値計算 |
| `ZobristKeys` | ハッシュキーの乱数テーブル |
| `TTEntry` | 置換表エントリ（評価値、BoundType、最善手） |

### Evaluates（局面評価）

| クラス | 責務 |
|--------|------|
| `FeaturePatternEvaluator` | 特徴パターンによる局面評価。13 種類のパターンを使用 |
| `FeaturePatternExtractor` | 盤面から特徴パターンを抽出 |
| `DiscCountEvaluator` | 石数差による単純評価（終盤用） |

### Movers（指し手決定）

| クラス | 責務 |
|--------|------|
| `FindBestMover` | 探索エンジンを使用して最善手を決定 |
| `FindFirstMover` | 最初に見つかった合法手を返す |

### Updaters（状態更新）

| クラス | 責務 |
|--------|------|
| `MoveAndReverseUpdater` | 着手と石の裏返しを実行 |
| `MobilityUpdater` | 着手可能位置を更新 |
| `InitializeUpdater` | ゲーム初期化 |

## 技術スタック

| カテゴリ | 技術 |
|----------|------|
| 言語 | C# 12 |
| フレームワーク | .NET 8.0 |
| DI コンテナ | Microsoft.Extensions.DependencyInjection |
| ログ基盤 | Serilog + Microsoft.Extensions.Logging |
| シリアライズ | Newtonsoft.Json |
| テスト | MSTest |
| UI | Windows Forms |

## 依存性注入

`DiProvider` クラスが静的サービスプロバイダを提供する。主要なサービスは Singleton として登録され、探索エンジンは Transient として登録される。

```csharp
// 使用例
var evaluator = DiProvider.Get().GetService<FeaturePatternEvaluator>();
var mover = DiProvider.Get().GetService<FindBestMover>();
```
