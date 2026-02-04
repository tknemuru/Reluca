# [RFC] WinForms盤面表示のDPIスケーリング対応

| 項目 | 内容 |
| :--- | :--- |
| **作成者 (Author)** | AI |
| **ステータス** | Draft (起草中) |
| **作成日** | 2026-02-04 |
| **タグ** | Reluca.Ui.WinForms, UI, バグ修正 |
| **関連リンク** | - |

## 1. 要約 (Summary)

- WinFormsアプリケーション（`BoardForm`）において、高DPI環境で盤面が二重に表示される問題を修正する。
- 原因は、Designer配置のコントロールがDPIスケーリングで自動拡大される一方、動的に生成される石のPictureBoxがハードコード座標のままであるため。
- 解決策として、石のPictureBox座標を`BoardPictureBox`の実際のサイズに基づいて動的に計算する。

## 2. 背景・動機 (Motivation)

### 現象

ゲーム開始時に、サイズの異なる盤面が二つ重なって表示される。具体的には：

- 背景に大きく引き伸ばされた緑の盤面画像
- その左上に小さくまとまった石のPictureBox群

### 原因

`BoardForm.Designer.cs`の設定：

```csharp
AutoScaleDimensions = new SizeF(10F, 25F);
AutoScaleMode = AutoScaleMode.Font;
```

この設定により、Designerで配置されたコントロール（`BoardPictureBox`など）は`InitializeComponent()`内でフォームのDPIスケーリング処理によって自動的に拡大される。

一方、`BoardForm.cs`の`Start()`メソッドで動的に生成される石のPictureBoxは、スケーリング処理の**後**に追加されるため、以下のハードコード座標がそのまま使用される：

```csharp
picture.Location = new Point(43 + (121 * BoardAccessor.GetColumnIndex(i)), 51 + (121 * BoardAccessor.GetRowIndex(i)));
picture.Size = new Size(114, 114);
```

### 影響

- 高DPI環境（150%など）で盤面が正しく表示されない
- ゲームのプレイに支障をきたす

## 3. 目的とスコープ (Goals & Non-Goals)

### 目的 (Goals)

- どのDPIスケーリング設定でも盤面が正しく表示されるようにする
- 石のPictureBoxが盤面の正しい位置に配置される

### やらないこと (Non-Goals)

- 盤面画像リソース（Board.gif）の高解像度化
- WinForms以外のUI実装（WPF、MAUI等への移行）
- Per-Monitor DPI対応（動的なDPI変更への追従）

## 4. 前提条件・依存関係 (Prerequisites & Dependencies)

- .NET 8.0 Windows Forms
- 既存の`BoardPictureBox`コントロール（Designer配置）

## 5. 詳細設計 (Detailed Design)

### 5.1 変更対象

- `Reluca.Ui.WinForms/BoardForm.cs` の `Start()` メソッド

### 5.2 修正内容

`BoardPictureBox`の実行時の位置とサイズから、石のPictureBoxの配置を動的に計算する。

#### 修正前（現行コード）

```csharp
DiscPictures = new List<PictureBox>();
for (var i = 0; i < Board.AllLength; i++)
{
    var picture = new PictureBox();
    picture.Image = Properties.Resources.Black;
    picture.Location = new Point(43 + (121 * BoardAccessor.GetColumnIndex(i)), 51 + (121 * BoardAccessor.GetRowIndex(i)));
    picture.Name = $"{DiscPictureNamePrefix}{i}";
    picture.Size = new Size(114, 114);
    // ...
}
```

#### 修正後

```csharp
DiscPictures = new List<PictureBox>();

// BoardPictureBoxの実際の位置とサイズを取得
var boardBounds = BoardPictureBox.Bounds;
var cellWidth = boardBounds.Width / 8.0;
var cellHeight = boardBounds.Height / 8.0;
var discSize = (int)(Math.Min(cellWidth, cellHeight) * 0.9);  // セルの90%程度のサイズ
var offsetX = (cellWidth - discSize) / 2;  // セル内の中央配置用オフセット
var offsetY = (cellHeight - discSize) / 2;

for (var i = 0; i < Board.AllLength; i++)
{
    var picture = new PictureBox();
    picture.Image = Properties.Resources.Black;

    var col = BoardAccessor.GetColumnIndex(i);
    var row = BoardAccessor.GetRowIndex(i);

    // BoardPictureBoxの位置とサイズに基づいて計算
    var x = boardBounds.Left + (int)(col * cellWidth + offsetX);
    var y = boardBounds.Top + (int)(row * cellHeight + offsetY);

    picture.Location = new Point(x, y);
    picture.Name = $"{DiscPictureNamePrefix}{i}";
    picture.Size = new Size(discSize, discSize);
    picture.SizeMode = PictureBoxSizeMode.StretchImage;
    picture.TabIndex = 5 + i;
    picture.TabStop = false;
    picture.Click += DiscPictureBox_Click;
    DiscPictures.Add(picture);
    Controls.Add(picture);
    picture.BringToFront();
}
```

### 5.3 計算の詳細

| 変数 | 計算式 | 説明 |
|------|--------|------|
| `boardBounds` | `BoardPictureBox.Bounds` | スケーリング後の実際の位置・サイズ |
| `cellWidth` | `boardBounds.Width / 8.0` | 1マスの幅 |
| `cellHeight` | `boardBounds.Height / 8.0` | 1マスの高さ |
| `discSize` | `min(cellWidth, cellHeight) * 0.9` | 石のサイズ（セルの90%） |
| `offsetX/Y` | `(cellSize - discSize) / 2` | セル内中央配置のオフセット |
| `x` | `boardBounds.Left + col * cellWidth + offsetX` | 石のX座標 |
| `y` | `boardBounds.Top + row * cellHeight + offsetY` | 石のY座標 |

## 6. 代替案の検討 (Alternatives Considered)

### 案A: BoardPictureBoxのBoundsから動的計算（採用案）

- **概要**: 実行時の`BoardPictureBox.Bounds`から石の位置を動的に計算する
- **長所**:
  - DPIスケーリングに自動対応
  - 盤面サイズの変更にも自動対応
  - コード変更が局所的
- **短所**:
  - 毎回計算が必要（ただし初期化時のみなので影響軽微）

### 案B: DPIスケーリングファクターを取得して座標を補正

- **概要**: `DeviceDpi / 96f`でスケーリングファクターを取得し、ハードコード座標に乗算する
- **長所**:
  - 既存の座標計算ロジックを活かせる
- **短所**:
  - DPIスケーリングの仕組みに依存
  - Per-Monitor DPI環境での挙動が複雑
  - `BoardPictureBox`のサイズが変わると再調整が必要

### 案C: 石のPictureBoxをDesignerで事前配置

- **概要**: 64個の石用PictureBoxをDesignerで配置し、スケーリング対象にする
- **長所**:
  - 自動スケーリングの恩恵を受けられる
- **短所**:
  - Designer.csが肥大化
  - 64コントロールの手動管理が煩雑
  - 動的な盤面サイズ変更に対応不可

### 選定理由

案Aを採用する。理由は以下の通り：

1. **堅牢性**: 実際のコントロールサイズに基づくため、DPIスケーリングの仕組みに依存しない
2. **保守性**: 将来的に盤面サイズを変更しても自動追従する
3. **シンプルさ**: 変更箇所が`Start()`メソッド内に閉じている

## 7. 横断的関心事 (Cross-Cutting Concerns)

### 7.1 セキュリティとプライバシー

該当なし。UI表示の修正のみ。

### 7.2 スケーラビリティとパフォーマンス

- 石の位置計算はゲーム開始時の1回のみ
- 64回の単純な算術演算であり、パフォーマンスへの影響は無視できる

### 7.3 可観測性 (Observability)

該当なし。

### 7.4 マイグレーションと後方互換性

- 破壊的変更なし
- 100% DPI環境での動作に変化なし（計算結果が現行のハードコード値と同等になる）

## 8. テスト戦略 (Test Strategy)

### 手動テスト

以下のDPIスケーリング設定で動作確認を行う：

| スケーリング | 確認項目 |
|--------------|----------|
| 100% | 現行と同等の表示になること |
| 125% | 盤面と石が正しく重なること |
| 150% | 盤面と石が正しく重なること |
| 175% | 盤面と石が正しく重なること |

### 確認観点

- 石が盤面の各マスの中央に配置されること
- 石のサイズがマスに対して適切であること
- クリック判定が正しく動作すること

## 9. 実装・リリース計画 (Implementation Plan)

### フェーズ1: 修正実装

1. `BoardForm.cs`の`Start()`メソッドを修正
2. ローカル環境で各DPIスケーリングでの動作確認

### フェーズ2: 検証・リリース

1. PRレビュー
2. mainブランチへマージ

### システム概要ドキュメントへの影響

- `docs/architecture.md`: 影響なし
- `docs/domain-model.md`: 影響なし
