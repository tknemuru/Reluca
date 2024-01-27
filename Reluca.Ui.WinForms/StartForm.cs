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
        public StartForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 「ひとりで遊ぶ」クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SinglePlayButton_Click(object sender, EventArgs e)
        {
            var boardForm = new BoardForm();
            boardForm.ShowDialog();
        }
    }
}
