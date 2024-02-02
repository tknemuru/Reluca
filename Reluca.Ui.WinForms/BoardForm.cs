using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;

namespace Reluca.Ui.WinForms
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
    /// <summary>
    /// 盤フォーム
    /// </summary>
    public partial class BoardForm : Form
    {
        /// <summary>
        /// 石のピクチャコントロール名の接頭辞
        /// </summary>
        private const string DiscPictureNamePrefix = "DiscPictureBox";

        /// <summary>
        /// 盤の状態に対応した画像
        /// </summary>
        private static Dictionary<Board.Status, Bitmap> StateImages = new Dictionary<Board.Status, Bitmap>()
        {
            [Board.Status.Empty] = Properties.Resources.Space,
            [Board.Status.Mobility] = Properties.Resources.Mobility,
            [Board.Status.Black] = Properties.Resources.Black,
            [Board.Status.White] = Properties.Resources.White,
        };

        /// <summary>
        /// 石のピクチャコントロールリスト
        /// </summary>
        private List<PictureBox> DiscPictures {  get; set; }

        /// <summary>
        /// ゲーム状態
        /// </summary>
        private GameContext Context { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BoardForm()
        {
            InitializeComponent();
            Start();
            UpdateForm();
        }

        private void Start()
        {
            DiscPictures = new List<PictureBox>();
            for (var i = 0; i < Board.AllLength; i++)
            {
                var picture = new PictureBox();
                picture.Image = Properties.Resources.Black;
                picture.Location = new Point(43 + (121 * BoardAccessor.GetColumnIndex(i)), 51 + (121 * BoardAccessor.GetRowIndex(i)));
                picture.Name = $"{DiscPictureNamePrefix}{i}";
                picture.Size = new Size(114, 114);
                picture.SizeMode = PictureBoxSizeMode.StretchImage;
                picture.TabIndex = 5 + i;
                picture.TabStop = false;
                picture.Click += DiscPictureBox_Click;
                DiscPictures.Add(picture);
                Controls.Add(picture);
                picture.BringToFront();
            }

            Context = new GameContext();
            DiProvider.Get().GetService<InitializeUpdater>().Update(Context);
            BoardAccessor.ChangeOppositeTurn(Context);
        }

        private void UpdateForm()
        {
            Context.TurnCount++;
            BoardAccessor.ChangeOppositeTurn(Context);
            DiProvider.Get().GetService<MobilityUpdater>().Update(Context);
            if (Context.Mobility <= 0)
            {
                BoardAccessor.ChangeOppositeTurn(Context);
                DiProvider.Get().GetService<MobilityUpdater>().Update(Context);
                if (Context.Mobility <= 0)
                {
                    // ゲーム終了
                }
            }
            BlackDiscCountLabel.Text = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.Black).ToString();
            WhiteDiscCountLabel.Text = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.White).ToString();
            foreach (var picture in DiscPictures)
            {
                var index = int.Parse(picture.Name.Replace(DiscPictureNamePrefix, string.Empty));
                var state = BoardAccessor.GetState(Context, index);
                picture.Image = StateImages[state];
            }

            // プレイヤ名の色
            if (Context.Turn == Disc.Color.Black)
            {
                BlackPlayerNameLabel.BackColor = SystemColors.Highlight;
                BlackPlayerNameLabel.ForeColor = SystemColors.HighlightText;
                WhitePlayerNameLabel.BackColor = SystemColors.Control;
                WhitePlayerNameLabel.ForeColor = SystemColors.ControlText;
            } else
            {
                BlackPlayerNameLabel.BackColor = SystemColors.Control;
                BlackPlayerNameLabel.ForeColor = SystemColors.ControlText;
                WhitePlayerNameLabel.BackColor = SystemColors.Highlight;
                WhitePlayerNameLabel.ForeColor = SystemColors.HighlightText;
            }
        }

        private void DiscPictureBox_Click(object sender, EventArgs e)
        {
            PictureBox picture = (PictureBox)sender;
            var index = int.Parse(picture.Name.Replace(DiscPictureNamePrefix, string.Empty));
            var state = BoardAccessor.GetState(Context, index);

            // 配置不可能なら何もしない
            if (state != Board.Status.Mobility)
            {
                return;
            }

            // 指定した場所に指す
            Context.Move = index;
            DiProvider.Get().GetService<MoveAndReverseUpdater>().Update(Context);

            // 画面を更新する
            UpdateForm();
        }
    }
}
