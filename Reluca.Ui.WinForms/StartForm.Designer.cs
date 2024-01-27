namespace Reluca.Ui.WinForms
{
    partial class StartForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SinglePlayButton = new Button();
            DoublePlayButton = new Button();
            SuspendLayout();
            // 
            // SinglePlayButton
            // 
            SinglePlayButton.Font = new Font("Yu Gothic UI", 18F);
            SinglePlayButton.Location = new Point(258, 69);
            SinglePlayButton.Name = "SinglePlayButton";
            SinglePlayButton.Size = new Size(269, 67);
            SinglePlayButton.TabIndex = 0;
            SinglePlayButton.Text = "ひとりで遊ぶ";
            SinglePlayButton.UseVisualStyleBackColor = true;
            SinglePlayButton.Click += SinglePlayButton_Click;
            // 
            // DoublePlayButton
            // 
            DoublePlayButton.Font = new Font("Yu Gothic UI", 18F);
            DoublePlayButton.Location = new Point(258, 218);
            DoublePlayButton.Name = "DoublePlayButton";
            DoublePlayButton.Size = new Size(269, 67);
            DoublePlayButton.TabIndex = 1;
            DoublePlayButton.Text = "ふたりで遊ぶ";
            DoublePlayButton.UseVisualStyleBackColor = true;
            // 
            // StartForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(795, 407);
            Controls.Add(DoublePlayButton);
            Controls.Add(SinglePlayButton);
            Name = "StartForm";
            Text = "StartForm";
            ResumeLayout(false);
        }

        #endregion

        private Button SinglePlayButton;
        private Button DoublePlayButton;
    }
}