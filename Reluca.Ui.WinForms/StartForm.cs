using Reluca.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reluca.Ui.WinForms
{
    public partial class StartForm : Form
    {
        /// <summary>
        /// 盤フォーム
        /// </summary>
        private BoardForm BoardForm { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public StartForm()
        {
            InitializeComponent();
            InfoLabel.Text = string.Empty;
            BoardForm = new BoardForm();
        }

        public void ShowResult(string message)
        {
            Show();
            InfoLabel.Text = message;
        }

        /// <summary>
        /// 「ひとりで遊ぶ」クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SinglePlayButton_Click(object sender, EventArgs e)
        {
            var players = new Dictionary<Disc.Color, Player.Type>()
            {
                [Disc.Color.Black] = Player.Type.Human,
                [Disc.Color.White] = Player.Type.Cpu
            };
            BoardForm.Start(this, players);
            BoardForm.Show();
        }

        /// <summary>
        /// 「ふたりで遊ぶ」クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoublePlayButton_Click(object sender, EventArgs e)
        {
            var players = new Dictionary<Disc.Color, Player.Type>()
            {
                [Disc.Color.Black] = Player.Type.Human,
                [Disc.Color.White] = Player.Type.Human
            };
            BoardForm.Start(this, players);
            BoardForm.Show();
        }

        /// <summary>
        /// 「対戦をみてる」クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoPlayButton_Click(object sender, EventArgs e)
        {
            var players = new Dictionary<Disc.Color, Player.Type>()
            {
                [Disc.Color.Black] = Player.Type.Cpu,
                [Disc.Color.White] = Player.Type.Cpu
            };
            BoardForm.Start(this, players);
            BoardForm.Show();
        }
    }
}
