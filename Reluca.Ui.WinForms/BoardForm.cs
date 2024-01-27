using Reluca.Accessors;
using Reluca.Contexts;
using Reluca.Di;
using Reluca.Models;
using Reluca.Updaters;

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
        }

        private void UpdateForm()
        {
            DiProvider.Get().GetService<MobilityUpdater>().Update(Context);
            foreach (var picture in DiscPictures)
            {
                var index = int.Parse(picture.Name.Replace(DiscPictureNamePrefix, string.Empty));
                var state = BoardAccessor.GetState(Context, index);
                picture.Image = StateImages[state];
            }
        }

        private void DiscPictureBox_Click(object sender, EventArgs e)
        {

        }
    }
}
