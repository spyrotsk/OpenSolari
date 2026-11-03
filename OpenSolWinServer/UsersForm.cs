using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenSol.Core;
using System.Collections.Generic;

namespace OpenSolWinServer
{
    public class UsersForm : Form
    {
        private ListBox lstUsers;
        private TextBox txtName;
        private TextBox txtCode;
        private Button btnGenerateCode;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnClose;
        private Label lblName;
        private Label lblCode;
        private CheckBox chkIgnoreGeolock;
        private Label lblDevices;
        private TextBox txtDevices;

        public UsersForm()
        {
            InitializeComponent();
            RefreshList();
        }

        private CheckBox[] chkDays;
        private DateTimePicker[] dtpStarts;
        private DateTimePicker[] dtpEnds;
        private Button btnEdit;
        private Button btnSave;

        private void InitializeComponent()
        {
            this.Text = "User Management";
            this.Size = new Size(600, 660); 
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblName = new Label() { Text = "Username:", Location = new Point(20, 20), AutoSize = true };
            txtName = new TextBox() { Location = new Point(20, 40), Width = 150 };

            lblCode = new Label() { Text = "Secret Code (empty = keep):", Location = new Point(180, 20), AutoSize = true };
            txtCode = new TextBox() { Location = new Point(180, 40), Width = 100 };
            btnGenerateCode = new Button() { Text = "Generate", Location = new Point(283, 38), Size = new Size(62, 24) };
            btnGenerateCode.Click += BtnGenerateCode_Click;

            chkIgnoreGeolock = new CheckBox() { Text = "Ignore Geolock (Admin/Special)", Location = new Point(350, 40), AutoSize = true };

            // Schedule Controls
            var grpSchedule = new GroupBox() { Text = "Access Schedule", Location = new Point(20, 70), Size = new Size(540, 260) };
            
            // Header Labels
            var lblDay = new Label() { Text = "Day", Location = new Point(20, 20), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            var lblStart = new Label() { Text = "Start (HH:mm)", Location = new Point(150, 20), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            var lblEnd = new Label() { Text = "End (HH:mm)", Location = new Point(270, 20), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };

            grpSchedule.Controls.Add(lblDay);
            grpSchedule.Controls.Add(lblStart);
            grpSchedule.Controls.Add(lblEnd);

            string[] dayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            string[] dayKeys = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            
            chkDays = new CheckBox[7];
            dtpStarts = new DateTimePicker[7];
            dtpEnds = new DateTimePicker[7];
            
            int yPos = 50;
            for (int i = 0; i < 7; i++)
            {
                // Checkbox for Day
                chkDays[i] = new CheckBox() { Text = dayNames[i], Tag = dayKeys[i], Location = new Point(20, yPos), AutoSize = true };
                
                // Start Time
                dtpStarts[i] = new DateTimePicker() { 
                    Format = DateTimePickerFormat.Custom, 
                    CustomFormat = "HH:mm", 
                    ShowUpDown = true, 
                    Location = new Point(150, yPos-3), 
                    Width = 80, 
                    Value = DateTime.Today.AddHours(8),
                    Enabled = false
                };

                // End Time
                dtpEnds[i] = new DateTimePicker() { 
                    Format = DateTimePickerFormat.Custom, 
                    CustomFormat = "HH:mm", 
                    ShowUpDown = true, 
                    Location = new Point(270, yPos-3), 
                    Width = 80, 
                    Value = DateTime.Today.AddHours(18),
                    Enabled = false
                };

                // Event to toggle enabling
                int index = i; // capture variable
                chkDays[i].CheckedChanged += (s, e) => {
                    bool isChecked = chkDays[index].Checked;
                    dtpStarts[index].Enabled = isChecked;
                    dtpEnds[index].Enabled = isChecked;
                };

                // Default: Unchecked
                chkDays[i].Checked = false;

                grpSchedule.Controls.Add(chkDays[i]);
                grpSchedule.Controls.Add(dtpStarts[i]);
                grpSchedule.Controls.Add(dtpEnds[i]);
                
                yPos += 30;
            }

            lblDevices = new Label() { Text = "Allowed devices (IDs separated by commas, empty = all):", Location = new Point(20, 340), AutoSize = true };
            txtDevices = new TextBox() { Location = new Point(20, 358), Width = 540 };

            btnSave = new Button() { Text = "Save User", Location = new Point(20, 390), Width = 310 };
            btnSave.Click += BtnSave_Click;

            lstUsers = new ListBox() { Location = new Point(20, 430), Width = 540, Height = 140 };

            btnEdit = new Button() { Text = "Edit Selected", Location = new Point(340, 390), Width = 220 };
            btnEdit.Click += BtnEdit_Click;

            btnRemove = new Button() { Text = "Remove Selected", Location = new Point(20, 580), Width = 150 };
            btnRemove.Click += BtnRemove_Click;

            btnClose = new Button() { Text = "Close", Location = new Point(410, 580), Width = 150 };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblCode);
            this.Controls.Add(txtCode);
            this.Controls.Add(btnGenerateCode);
            this.Controls.Add(grpSchedule);
            this.Controls.Add(chkIgnoreGeolock);
            this.Controls.Add(lblDevices);
            this.Controls.Add(txtDevices);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnEdit);
            this.Controls.Add(lstUsers);
            this.Controls.Add(btnRemove);
            this.Controls.Add(btnClose);
        }

        private void RefreshList()
        {
            lstUsers.Items.Clear();
            var config = AccessControlManager.LoadConfig();
            foreach (var user in config.Users)
            {
                string info = $"{user.Username}";
                if (user.IgnoreSchedule) info += " (Admin/Always)";
                else info += " (Schedule)";

                if (user.IgnoreGeolock) info += " (No Geolock)";

                if (user.AllowedDevices != null && !user.AllowedDevices.Contains("*"))
                    info += user.AllowedDevices.Count == 0 ? " (Devices: NONE)" : $" (Devices: {string.Join(", ", user.AllowedDevices)})";
                
                lstUsers.Items.Add(new UserItem { Name = user.Username, Token = user.Token, Display = info });
            }
        }

        private Dictionary<string, string> GetScheduleFromUI()
        {
            var schedule = new Dictionary<string, string>();
            
            for (int i = 0; i < 7; i++)
            {
                if (chkDays[i].Checked)
                {
                    string start = dtpStarts[i].Value.ToString("HH:mm");
                    string end = dtpEnds[i].Value.ToString("HH:mm");
                    string timeRange = $"{start}-{end}";
                    
                    schedule[chkDays[i].Tag.ToString()] = timeRange;
                }
            }
            return schedule;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string code = txtCode.Text.Trim();

            var config = AccessControlManager.LoadConfig();
            bool exists = config.Users.Any(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));

            // The code is only required for new users: an empty code keeps the existing one
            if (string.IsNullOrEmpty(name) || (string.IsNullOrEmpty(code) && !exists))
            {
                MessageBox.Show("Please enter both username and code.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(code) && code.Length < SecurityUtils.MinCodeLength)
            {
                MessageBox.Show($"The code must be at least {SecurityUtils.MinCodeLength} characters long. Use \"Generate\" to create a random one.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Logic change: If we are editing (name matches existing), we update.
            // If name is new, it's a new user. 
            // The prompt says "Modify a user... press Save". 
            // We can just rely on AddUser usage which updates if exists.
            
            // Only confirm if it's an update IS tricky because we don't know if the user INTENDED to update or just typed a name that exists.
            // But for "Edit" flow, the name is pre-filled.
            // Let's keep the confirmation but make it softer? Or remove it if likely editing?
            // User pressed "Save". Let's assume standard behavior.
            
            if (exists)
            {
                // Verify if we should prompt. For now, strict "Confirm" is safe.
                if (MessageBox.Show("User updates will be overwritten. Continue?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.No)
                    return;
            }

            var schedule = GetScheduleFromUI();
            
            bool ignoreSchedule = false; 
            if (schedule.Count == 0)
            {
                 if (MessageBox.Show("No days selected. Grant FULL 24/7 ACCESS (Admin)?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                 {
                     ignoreSchedule = true;
                 }
                 else
                 {
                     return; 
                 }
            }

            // Empty field = all devices
            var devices = txtDevices.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            List<string>? allowedDevices = devices.Count == 0 ? null : devices;

            AccessControlManager.AddUser(name, code, schedule, ignoreSchedule, chkIgnoreGeolock.Checked, allowedDevices);
            
            txtName.Text = "";
            txtCode.Text = "";
            txtDevices.Text = "";
            chkIgnoreGeolock.Checked = false;
            // Reset UI
             foreach(var chk in chkDays) chk.Checked = false;

            RefreshList();
        }

        private void BtnGenerateCode_Click(object? sender, EventArgs e)
        {
            txtCode.Text = SecurityUtils.GenerateCode();
            try { Clipboard.SetText(txtCode.Text); } catch { }
            MessageBox.Show("A random code has been created and copied to the clipboard.\nGive it to the user now: once saved, it is stored as a hash and cannot be shown again.", "Code", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem is UserItem item)
            {
                var config = AccessControlManager.LoadConfig();
                var user = config.Users.FirstOrDefault(u => u.Username.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                if (user != null)
                {
                    txtName.Text = user.Username;
                    // Codes are stored as hashes and cannot be shown: leave empty to keep the current one
                    txtCode.Text = ""; 
                    
                    // Reset Checkboxes
                    foreach(var chk in chkDays) chk.Checked = false;

                    chkIgnoreGeolock.Checked = user.IgnoreGeolock;
                    txtDevices.Text = user.AllowedDevices == null ? "" : string.Join(", ", user.AllowedDevices);

                    if (!user.IgnoreSchedule && user.Schedule != null)
                    {
                         foreach(var key in user.Schedule.Keys)
                         {
                             // Find index for day key (Monday, Tuesday...)
                             for(int i=0; i<7; i++)
                             {
                                 if (chkDays[i].Tag.ToString() == key)
                                 {
                                     chkDays[i].Checked = true;
                                     string range = user.Schedule[key]; // "08:00-18:00"
                                     var parts = range.Split('-');
                                     if (parts.Length == 2)
                                     {
                                         if (DateTime.TryParse(parts[0], out DateTime start))
                                             dtpStarts[i].Value = start;
                                         
                                         if (DateTime.TryParse(parts[1], out DateTime end))
                                             dtpEnds[i].Value = end;
                                     }
                                 }
                             }
                         }
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a user to edit.", "Info");
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (lstUsers.SelectedItem is UserItem item)
            {
                if (MessageBox.Show($"Remove user {item.Name}?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    AccessControlManager.RemoveUser(item.Name);
                    RefreshList();
                }
            }
        }

        private class UserItem
        {
            public string Name { get; set; } = "";
            public string Token { get; set; } = ""; 
            public string Display {get; set; } = "";

            public override string ToString()
            {
                return Display;
            }
        }


    }
}
