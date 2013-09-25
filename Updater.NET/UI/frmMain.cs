using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NETUpdater.Update;
using NETUpdater.UI;

namespace NETUpdater.UI
{
    public partial class frmMain : Form, IUpdaterView
    {
        private Updater updater;
        public frmMain()
        {
            InitializeComponent();
            updater = new Updater(this as IUpdaterView);
            updater.SetArgs(Environment.GetCommandLineArgs());
        }
        #region IUpdaterView Members

        public string ProcessStatus
        {
            set { lblStatus.Text = value; }
        }

        public bool EnabledForm
        {
            set { Enabled = value; }
        }

        public bool DisableFormClosing { get; set; }

        public void CloseForm()
        {
            Close();
        }

        public void ShowForm()
        {
            Activate();
        }

        #endregion

        private void frmMain_Load(object sender, EventArgs e)
        {
            
            updater.BeginUpdate();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (!DisableFormClosing)
                Application.Exit();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            switch (e.CloseReason)
            {
                case CloseReason.TaskManagerClosing:
                case CloseReason.WindowsShutDown:
                    e.Cancel = false;
                    break;
                default:
                    e.Cancel = DisableFormClosing;
                    break;
            }

        }
    }
}
