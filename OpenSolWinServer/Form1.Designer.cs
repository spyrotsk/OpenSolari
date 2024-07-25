
namespace OpenSolWinServer
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
            components = new System.ComponentModel.Container();
            btnStartStop = new Button();
            lblStatus = new Label();
            txtPort = new TextBox();
            label1 = new Label();
            grpConfig = new GroupBox();
            chkUseHttps = new CheckBox();
            chkLogs = new CheckBox();
            grpLogs = new GroupBox();
            txtLogs = new TextBox();
            notifyIcon1 = new NotifyIcon(components);
            grpConfig.SuspendLayout();
            grpLogs.SuspendLayout();
            SuspendLayout();
            // 
            // btnStartStop
            // 
            btnStartStop.Location = new Point(12, 12);
            btnStartStop.Name = "btnStartStop";
            btnStartStop.Size = new Size(100, 30);
            btnStartStop.TabIndex = 0;
            btnStartStop.Text = "Start Server";
            btnStartStop.UseVisualStyleBackColor = true;
            btnStartStop.Click += btnStartStop_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblStatus.ForeColor = Color.Red;
            lblStatus.Location = new Point(129, 15);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(73, 21);
            lblStatus.TabIndex = 1;
            lblStatus.Text = "Stopped";
            // 
            // txtPort
            // 
            txtPort.Location = new Point(350, 16);
            txtPort.Name = "txtPort";
            txtPort.Size = new Size(60, 23);
            txtPort.TabIndex = 2;
            txtPort.Text = "8080";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(308, 19);
            label1.Name = "label1";
            label1.Size = new Size(38, 15);
            label1.TabIndex = 3;
            label1.Text = "Port:";
            // 
            // grpConfig
            // 
            grpConfig.Controls.Add(chkLogs);
            grpConfig.Controls.Add(chkUseHttps);
            grpConfig.Location = new Point(12, 60);
            grpConfig.Name = "grpConfig";
            grpConfig.Size = new Size(627, 134);
            grpConfig.TabIndex = 4;
            grpConfig.TabStop = false;
            grpConfig.Text = "Configuration";
            // 
            // chkUseHttps
            // 
            chkUseHttps.AutoSize = true;
            chkUseHttps.Location = new Point(170, 29); // Positioned next to the "Manage Users" button that will be added dynamically
            chkUseHttps.Name = "chkUseHttps";
            chkUseHttps.Size = new Size(97, 19);
            chkUseHttps.TabIndex = 0;
            chkUseHttps.Text = "Enable HTTPS";
            chkUseHttps.UseVisualStyleBackColor = true;
            // 
            // chkLogs
            // 
            chkLogs.AutoSize = true;
            chkLogs.Location = new Point(chkUseHttps.Right + 20, 29); // Distanziato
            chkLogs.Name = "chkLogs";
            chkLogs.Size = new Size(110, 19);
            chkLogs.TabIndex = 1;
            chkLogs.Text = "Enable Log Files";
            chkLogs.UseVisualStyleBackColor = true;
            //  
            // grpLogs
            // 
            grpLogs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grpLogs.Controls.Add(txtLogs);
            grpLogs.Location = new Point(12, 200);
            grpLogs.Name = "grpLogs";
            grpLogs.Size = new Size(630, 330);
            grpLogs.TabIndex = 5;
            grpLogs.TabStop = false;
            grpLogs.Text = "Logs";
            // 
            // txtLogs
            // 
            txtLogs.Dock = DockStyle.Fill;
            txtLogs.Location = new Point(3, 19);
            txtLogs.Multiline = true;
            txtLogs.Name = "txtLogs";
            txtLogs.ReadOnly = true;
            txtLogs.ScrollBars = ScrollBars.Vertical;
            txtLogs.Size = new Size(624, 308);
            txtLogs.TabIndex = 0;
            // 
            // notifyIcon1
            // 
            notifyIcon1.Text = "OpenSol Server";
            notifyIcon1.Visible = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(654, 541);
            Controls.Add(grpLogs);
            Controls.Add(grpConfig);
            Controls.Add(label1);
            Controls.Add(txtPort);
            Controls.Add(lblStatus);
            Controls.Add(btnStartStop);
            Name = "Form1";
            Text = "OpenSol Server";
            Load += Form1_Load;
            grpConfig.ResumeLayout(false);
            grpConfig.PerformLayout();
            grpLogs.ResumeLayout(false);
            grpLogs.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStartStop;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox grpConfig;
        private System.Windows.Forms.CheckBox chkUseHttps;
        private System.Windows.Forms.CheckBox chkLogs;
        private System.Windows.Forms.GroupBox grpLogs;
        private System.Windows.Forms.TextBox txtLogs;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
    }
}
