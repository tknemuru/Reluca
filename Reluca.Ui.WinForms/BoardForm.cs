using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;
using System;
using System.Reflection;

namespace Reluca.Ui.WinForms
{
#pragma warning disable CS8602 // null �Q�Ƃ̉\����������̂̋t�Q�Ƃł��B
#pragma warning disable CS8618 // null �񋖗e�̃t�B�[���h�ɂ́A�R���X�g���N�^�[�̏I������ null �ȊO�̒l�������Ă��Ȃ���΂Ȃ�܂���BNull ���e�Ƃ��Đ錾���邱�Ƃ����������������B
#pragma warning disable CS8622
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
        /// �N�����t�H�[��
        /// </summary>
        private StartForm StartForm { get; set; }

        /// <summary>
        /// �v���C�����X�g
        /// </summary>
        private Dictionary<Disc.Color, Player.Type> Players { get; set; }

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
        }

        /// <summary>
        /// �Q�[�����J�n���܂��B
        /// </summary>
        /// <param name="sender">�N�����t�H�[��</param>
        /// <param name="players">�v���C�����X�g</param>
        public void Start(StartForm sender, Dictionary<Disc.Color, Player.Type> players)
        {
            StartForm = sender;
            Players = players;
            StartForm.Hide();

            if (Players.Values.Contains(Player.Type.Cpu))
            {
                BlackPlayerNameLabel.Text = "���F���Ȃ�";
                WhitePlayerNameLabel.Text = "���FCPU";
            } else
            {
                BlackPlayerNameLabel.Text = "���F�v���C��1";
                WhitePlayerNameLabel.Text = "���F�v���C��2";
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
        /// �t�H�[����ʂ��A�b�v�f�[�g���܂��B
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
                    // �Q�[���I��
                    End();
                    return;
                }
            }

            //if (Players[Context.Turn] == Player.Type.Cpu)
            //{
            //    // TODO: CPU���ł�
            //    Context.Move = index;
            //    DiProvider.Get().GetService<MoveAndReverseUpdater>().Update(Context);
            //    Next();
            //}
        }

        /// <summary>
        /// �t�H�[����ʂ����t���b�V�����܂��B
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

            // �v���C�����̐F
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
        /// �Q�[�����I�����܂��B
        /// </summary>
        private void End()
        {
            var black = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.Black);
            var white = BoardAccessor.GetDiscCount(Context.Board, Disc.Color.White);
            if (black == white)
            {
                StartForm.ShowResult("���������ł�");
            }
            else
            {
                if (black > white)
                {
                    StartForm.ShowResult($"{BlackPlayerNameLabel.Text}�̏����ł�");
                }
                else
                {
                    StartForm.ShowResult($"{WhitePlayerNameLabel.Text}�̏����ł�");
                }
            }
        }

        /// <summary>
        /// �Ղ̃}�X�ڂ��N���b�N�����ۂɎ��s���܂��B
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            // �^�[������
            Next();
        }
    }
}
