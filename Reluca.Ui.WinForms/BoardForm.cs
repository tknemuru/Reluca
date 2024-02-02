using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;
using System.Reflection;

namespace Reluca.Ui.WinForms
{
#pragma warning disable CS8602 // null 参照の可能性があるものの逆参照です。
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
#pragma warning disable CS8622
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
        /// 起動元フォーム
        /// </summary>
        private StartForm StartForm { get; set; }

        /// <summary>
        /// プレイヤリスト
        /// </summary>
        private Dictionary<Disc.Color, Player.Type> Players { get; set; }

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
        }

        /// <summary>
        /// ゲームを開始します。
        /// </summary>
        /// <param name="sender">起動元フォーム</param>
        /// <param name="players">プレイヤリスト</param>
        public void Start(StartForm sender, Dictionary<Disc.Color, Player.Type> players)
        {
            StartForm = sender;
            Players = players;
            StartForm.Hide();

            if (Players.Values.Contains(Player.Type.Cpu))
            {
                BlackPlayerNameLabel.Text = "黒：あなた";
                WhitePlayerNameLabel.Text = "白：CPU";
            } else
            {
                BlackPlayerNameLabel.Text = "黒：プレイヤ1";
                WhitePlayerNameLabel.Text = "白：プレイヤ2";
            }

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
            Next();
        }

        /// <summary>
        /// フォーム画面をアップデートします。
        /// </summary>
        private void Next()
        {
            Context.TurnCount++;
            BoardAccessor.ChangeOppositeTurn(Context);
            RefreshForm();
            if (Context.Mobility <= 0)
            {
                BoardAccessor.ChangeOppositeTurn(Context);
                RefreshForm();
                if (Context.Mobility <= 0)
                {
                    // ゲーム終了
                    End();
                    return;
                }
            }

            //if (Players[Context.Turn] == Player.Type.Cpu)
            //{
            //    // TODO: CPUが打つ
            //    Context.Move = index;
            //    DiProvider.Get().GetService<MoveAndReverseUpdater>().Update(Context);
            //    Next();
            //}
        }

        /// <summary>
        /// フォーム画面をリフレッシュします。
        /// </summary>
        private void RefreshForm()
        {
            DiProvider.Get().GetService<MobilityUpdater>().Update(Context);
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
            }
            else
            {
                BlackPlayerNameLabel.BackColor = SystemColors.Control;
                BlackPlayerNameLabel.ForeColor = SystemColors.ControlText;
                WhitePlayerNameLabel.BackColor = SystemColors.Highlight;
                WhitePlayerNameLabel.ForeColor = SystemColors.HighlightText;
            }
        }

        /// <summary>
        /// ゲームを終了します。
        /// </summary>
        private void End()
        {
            var black = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.Black);
            var white = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.White);
            if (black == white)
            {
                StartForm.ShowResult("引き分けです");
            }
            else
            {
                if (black > white)
                {
                    StartForm.ShowResult($"{BlackPlayerNameLabel.Text}の勝ちです");
                }
                else
                {
                    StartForm.ShowResult($"{WhitePlayerNameLabel.Text}の勝ちです");
                }
            }
        }

        /// <summary>
        /// 盤のマス目をクリックした際に実行します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            // ターンを回す
            Next();
        }
    }
}
