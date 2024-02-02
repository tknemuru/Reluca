namespace Reluca.Ui.WinForms
{
    partial class BoardForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            BoardPictureBox = new PictureBox();
            BlackPlayerNameLabel = new Label();
            BlackDiscCountLabel = new Label();
            WhiteDiscCountLabel = new Label();
            WhitePlayerNameLabel = new Label();
            ((System.ComponentModel.ISupportInitialize)BoardPictureBox).BeginInit();
            SuspendLayout();
            // 
            // BoardPictureBox
            // 
            BoardPictureBox.Image = Properties.Resources.Board;
            BoardPictureBox.Location = new Point(24, 30);
            BoardPictureBox.Name = "BoardPictureBox";
            BoardPictureBox.Size = new Size(1000, 1000);
            BoardPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            BoardPictureBox.TabIndex = 0;
            BoardPictureBox.TabStop = false;
            // 
            // BlackPlayerNameLabel
            // 
            BlackPlayerNameLabel.AutoSize = true;
            BlackPlayerNameLabel.BackColor = SystemColors.Highlight;
            BlackPlayerNameLabel.Font = new Font("Yu Gothic UI", 22F);
            BlackPlayerNameLabel.ForeColor = SystemColors.HighlightText;
            BlackPlayerNameLabel.Location = new Point(1056, 55);
            BlackPlayerNameLabel.Name = "BlackPlayerNameLabel";
            BlackPlayerNameLabel.Size = new Size(246, 60);
            BlackPlayerNameLabel.TabIndex = 1;
            BlackPlayerNameLabel.Text = "黒プレイヤ名";
            // 
            // BlackDiscCountLabel
            // 
            BlackDiscCountLabel.AutoSize = true;
            BlackDiscCountLabel.Font = new Font("Yu Gothic UI", 34F);
            BlackDiscCountLabel.Location = new Point(1308, 34);
            BlackDiscCountLabel.Name = "BlackDiscCountLabel";
            BlackDiscCountLabel.Size = new Size(113, 91);
            BlackDiscCountLabel.TabIndex = 2;
            BlackDiscCountLabel.Text = "99";
            // 
            // WhiteDiscCountLabel
            // 
            WhiteDiscCountLabel.AutoSize = true;
            WhiteDiscCountLabel.Font = new Font("Yu Gothic UI", 34F);
            WhiteDiscCountLabel.Location = new Point(1308, 147);
            WhiteDiscCountLabel.Name = "WhiteDiscCountLabel";
            WhiteDiscCountLabel.Size = new Size(113, 91);
            WhiteDiscCountLabel.TabIndex = 4;
            WhiteDiscCountLabel.Text = "99";
            // 
            // WhitePlayerNameLabel
            // 
            WhitePlayerNameLabel.AutoSize = true;
            WhitePlayerNameLabel.Font = new Font("Yu Gothic UI", 22F);
            WhitePlayerNameLabel.Location = new Point(1056, 168);
            WhitePlayerNameLabel.Name = "WhitePlayerNameLabel";
            WhitePlayerNameLabel.Size = new Size(246, 60);
            WhitePlayerNameLabel.TabIndex = 3;
            WhitePlayerNameLabel.Text = "白プレイヤ名";
            // 
            // BoardForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1445, 1046);
            Controls.Add(WhiteDiscCountLabel);
            Controls.Add(WhitePlayerNameLabel);
            Controls.Add(BlackDiscCountLabel);
            Controls.Add(BlackPlayerNameLabel);
            Controls.Add(BoardPictureBox);
            Name = "BoardForm";
            Text = "リバーシ";
            ((System.ComponentModel.ISupportInitialize)BoardPictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox BoardPictureBox;
        private Label BlackPlayerNameLabel;
        private Label BlackDiscCountLabel;
        private Label WhiteDiscCountLabel;
        private Label WhitePlayerNameLabel;
    }
}
