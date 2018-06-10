using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace FolderMove
{
    public partial class frmProgress : Form
    {
        internal BackgroundWorker Worker;

        public frmProgress(BackgroundWorker worker)
        {
            InitializeComponent();

            Worker = worker;
            worker.ProgressChanged += Worker_ProgressChanged;
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            bar.Value = e.ProgressPercentage;
            bar.Update();
            label.Text = e.UserState.ToString();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Worker.CancelAsync();
        }
    }
}
