using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;

namespace Reluca.Ui.WinForms
{
#pragma warning disable CS8602 // null �Q�Ƃ̉\����������̂̋t�Q�Ƃł��B
    /// <summary>
    /// �Ճt�H�[��
    /// </summary>
    public partial class BoardForm : Form
    {
        /// <summary>
        /// �΂̃s�N�`���R���g���[�����̐ړ���
        /// </summary>
        private const string DiscPictureNamePrefix = "DiscPictureBox";

        /// <summary>
        /// �Ղ̏�ԂɑΉ������摜
        /// </summary>
        private static Dictionary<Board.Status, Bitmap> StateImages = new Dictionary<Board.Status, Bitmap>()
        {
            [Board.Status.Empty] = Properties.Resources.Space,
            [Board.Status.Mobility] = Properties.Resources.Mobility,
            [Board.Status.Black] = Properties.Resources.Black,
            [Board.Status.White] = Properties.Resources.White,
        };

        /// <summary>
        /// �΂̃s�N�`���R���g���[�����X�g
        /// </summary>
        private List<PictureBox> DiscPictures {  get; set; }

        /// <summary>
        /// �Q�[�����
        /// </summary>
        private GameContext Context { get; set; }

        /// <summary>
        /// �R���X�g���N�^
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
                    // �Q�[���I��
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

            // �v���C�����̐F
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

            // �z�u�s�\�Ȃ牽�����Ȃ�
            if (state != Board.Status.Mobility)
            {
                return;
            }

            // �w�肵���ꏊ�Ɏw��
            Context.Move = index;
            DiProvider.Get().GetService<MoveAndReverseUpdater>().Update(Context);

            // ��ʂ��X�V����
            UpdateForm();
        }
    }
}
