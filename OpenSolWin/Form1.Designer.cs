
namespace Cancello
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            grpConfig = new GroupBox();
            txtToken = new TextBox();
            label3 = new Label();
            btnSaveConfig = new Button();
            txtServerPort = new TextBox();
            label2 = new Label();
            txtServerIP = new TextBox();
            label1 = new Label();
            chkUseHttps = new CheckBox();
            grpConfig.SuspendLayout();
            SuspendLayout();
            // 
            // grpConfig
            // 
            grpConfig.Controls.Add(chkUseHttps);
            grpConfig.Controls.Add(txtToken);
            grpConfig.Controls.Add(label3);
            grpConfig.Controls.Add(btnSaveConfig);
            grpConfig.Controls.Add(txtServerPort);
            grpConfig.Controls.Add(label2);
            grpConfig.Controls.Add(txtServerIP);
            grpConfig.Controls.Add(label1);
            grpConfig.Location = new Point(12, 183);
            grpConfig.Name = "grpConfig";
            grpConfig.Size = new Size(187, 200); // Increased height to fit check box
            grpConfig.TabIndex = 3;
            grpConfig.TabStop = false;
            grpConfig.Text = "Server Configuration";
            // 
            // chkUseHttps
            // 
            chkUseHttps.AutoSize = true;
            chkUseHttps.Location = new Point(6, 153);
            chkUseHttps.Name = "chkUseHttps";
            chkUseHttps.Size = new Size(82, 19);
            chkUseHttps.TabIndex = 7;
            chkUseHttps.Text = "Use HTTPS";
            chkUseHttps.UseVisualStyleBackColor = true;
            // 
            // txtToken
            // 
            txtToken.Location = new Point(6, 125);
            txtToken.Name = "txtToken";
            txtToken.PasswordChar = '*';
            txtToken.Size = new Size(175, 23);
            txtToken.TabIndex = 6;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 107);
            label3.Name = "label3";
            label3.Size = new Size(42, 15);
            label3.TabIndex = 5;
            label3.Text = "Token:";
            // 
            // btnSaveConfig
            // 
            btnSaveConfig.Location = new Point(96, 80);
            btnSaveConfig.Name = "btnSaveConfig";
            btnSaveConfig.Size = new Size(85, 24);
            btnSaveConfig.TabIndex = 4;
            btnSaveConfig.Text = "Save";
            btnSaveConfig.UseVisualStyleBackColor = true;
            btnSaveConfig.Click += btnSaveConfig_Click;
            // 
            // txtServerPort
            // 
            txtServerPort.Location = new Point(6, 81);
            txtServerPort.Name = "txtServerPort";
            txtServerPort.Size = new Size(80, 23);
            txtServerPort.TabIndex = 3;
            txtServerPort.Text = "8080";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 63);
            label2.Name = "label2";
            label2.Size = new Size(38, 15);
            label2.TabIndex = 2;
            label2.Text = "Port:";
            // 
            // txtServerIP
            // 
            txtServerIP.Location = new Point(6, 37);
            txtServerIP.Name = "txtServerIP";
            txtServerIP.Size = new Size(175, 23);
            txtServerIP.TabIndex = 1;
            txtServerIP.Text = "127.0.0.1";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 19);
            label1.Name = "label1";
            label1.Size = new Size(55, 15);
            label1.TabIndex = 0;
            label1.Text = "Server IP:";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(211, 582);
            Controls.Add(grpConfig);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            MaximizeBox = false;
            Name = "Form1";
            SizeGripStyle = SizeGripStyle.Hide;
            Text = "Client";
            Load += Form1_Load;
            KeyDown += Form1_KeyDown;
            grpConfig.ResumeLayout(false);
            grpConfig.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpConfig;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.TextBox txtServerPort;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtServerIP;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtToken;
        private System.Windows.Forms.CheckBox chkUseHttps;
    }
}