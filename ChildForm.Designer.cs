namespace CheckLogic
{
    partial class ChildForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChildForm));
            this.PictBox = new System.Windows.Forms.PictureBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.testToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.HostPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.PictBox)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.HostPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // PictBox
            // 
            this.PictBox.AllowDrop = true;
            this.PictBox.BackColor = System.Drawing.SystemColors.Control;
            this.PictBox.Location = new System.Drawing.Point(0, 0);
            this.PictBox.Margin = new System.Windows.Forms.Padding(0);
            this.PictBox.Name = "PictBox";
            this.PictBox.Size = new System.Drawing.Size(698, 465);
            this.PictBox.TabIndex = 1;
            this.PictBox.TabStop = false;
            this.PictBox.DragDrop += new System.Windows.Forms.DragEventHandler(this.PictBox_DragDrop);
            this.PictBox.DragEnter += new System.Windows.Forms.DragEventHandler(this.PictBox_DragEnter);
            this.PictBox.Paint += new System.Windows.Forms.PaintEventHandler(this.PictBox_Paint);
            this.PictBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PictBox_MouseDown);
            this.PictBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.PictBox_MouseMove);
            this.PictBox.MouseUp += new System.Windows.Forms.MouseEventHandler(this.PictBox_MouseUp);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.testToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(94, 26);
            // 
            // testToolStripMenuItem
            // 
            this.testToolStripMenuItem.Name = "testToolStripMenuItem";
            this.testToolStripMenuItem.Size = new System.Drawing.Size(93, 22);
            this.testToolStripMenuItem.Text = "test";
            // 
            // HostPanel
            // 
            this.HostPanel.AutoScroll = true;
            this.HostPanel.Controls.Add(this.PictBox);
            this.HostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.HostPanel.Location = new System.Drawing.Point(0, 0);
            this.HostPanel.Name = "HostPanel";
            this.HostPanel.Size = new System.Drawing.Size(672, 449);
            this.HostPanel.TabIndex = 4;
            // 
            // ChildForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(672, 449);
            this.Controls.Add(this.HostPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "ChildForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.WindowsDefaultBounds;
            this.Text = "ChildForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ChildForm_FormClosing);
            this.Load += new System.EventHandler(this.ChildForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ChildForm_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ChildForm_KeyUp);
            ((System.ComponentModel.ISupportInitialize)(this.PictBox)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.HostPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox PictBox;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem testToolStripMenuItem;
        private System.Windows.Forms.Panel HostPanel;

    }
}