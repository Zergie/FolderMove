using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace FolderMove
{
    public partial class frmMain : Form
    {
        frmProgress progressWindow;

        public frmMain()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            FolderMoveAction action = new FolderMoveAction(txbSource.Text, txbDestination.Text);

            if (!Directory.Exists(action.SourcePath))
            {
                MessageBox.Show($"Source folder {action.SourcePath} does not exist.\n\nPlease choose an existing folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (Directory.Exists(action.DestinationPath)
                && (Directory.GetFiles(action.DestinationPath).Count() > 0 || Directory.GetDirectories(action.DestinationPath).Count() > 0))
            {
                MessageBox.Show($"Destination folder {action.DestinationPath} is not empty!\n\nPlease choose an empty folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                var worker = new BackgroundWorker
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true,
                };

                worker.DoWork += Worker_DoWork;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

                progressWindow = new frmProgress(worker);
                progressWindow.Left = Left;
                progressWindow.Top = Top;
                progressWindow.Show();
                Visible = false;

                worker.RunWorkerAsync(action);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = ((BackgroundWorker)sender);
            FolderMoveAction action = ((FolderMoveAction)e.Argument);
            FolderMoveResult result = new FolderMoveResult();
            e.Result = result;

            worker.ReportProgress(0, "calculation folder size");
            var folderCount = GetDirectoryCount(action.SourcePath);
            var fileSizes = GetDirectorySize(action.SourcePath);
            long progressMax = folderCount + fileSizes;
            long progress = 0;

            if (!Directory.Exists(action.DestinationPath))
            {
                worker.ReportProgress(0, $"creating folder {action.DestinationPath}");
                Directory.CreateDirectory(action.DestinationPath);
                result.SetFlag(enumFolderMoveResult.DeleteDestination);
            }

            // createing directory tree and copy files
            var directories = new Stack<string>();
            directories.Push(action.SourcePath);
            result.SetFlag(enumFolderMoveResult.CleanDestination);

            while (directories.Count > 0 && !worker.CancellationPending)
            {
                var basePath = directories.Pop();

                foreach (var d in Directory.EnumerateDirectories(basePath))
                {
                    var newDirectory = Path.Combine(action.DestinationPath, d.Substring(action.SourcePath.Length + 1));

                    worker.ReportProgress(CalcProgress(progress, progressMax), $"creating folder {newDirectory}");
                    Directory.CreateDirectory(newDirectory);
                    directories.Push(d);

                    progress++;

                    if (worker.CancellationPending) return;
                }

                foreach (var f in Directory.EnumerateFiles(basePath))
                {
                    var destPath = Path.Combine(action.DestinationPath, f.Substring(action.SourcePath.Length + 1));
                    FileInfo info = new FileInfo(f);

                    worker.ReportProgress(CalcProgress(progress, progressMax), $"copying to {destPath}");
                    File.Copy(f, destPath);

                    progress += info.Length;

                    if (worker.CancellationPending) return;
                }
            }


            // moveing old folder
            if (worker.CancellationPending) return;
            worker.ReportProgress(CalcProgress(progress, progressMax), $"renaming {Path.GetFileName(action.SourcePath)} to {Path.GetFileName(action.SourcePath)}.old ..");
            string sourceCopy = Path.Combine(Path.GetTempPath(), Path.GetFileName(action.SourcePath) + ".old");
            try
            {
                Directory.Move(action.SourcePath, sourceCopy);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                result.SetFlag(enumFolderMoveResult.RevertMoveSource);
            }

            // create link
            if (worker.CancellationPending) return;
            worker.ReportProgress(CalcProgress(progress, progressMax), $"creating symbolic link ..");
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c mklink /J \"{action.SourcePath}\" \"{action.DestinationPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.OutputDataReceived += Process_OutputDataReceived;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    result.SetFlag(enumFolderMoveResult.DeleteLink);

                process.Close();
            }

            // removeing old folder
            if (worker.CancellationPending) return;
            Directory.Delete(sourceCopy, true);

            // finishing
            if (worker.CancellationPending) return;
            result.Reset();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                progressWindow.Worker.ReportProgress(100, e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                MessageBox.Show(e.Data, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int CalcProgress(long current, long max)
        {
            return (int)(((double)current / (double)max) * 100);
        }

        private long GetDirectoryCount(string path)
        {
            long result = 0;
            foreach (var i in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            {
                result++;
            }

            return result;
        }

        private long GetDirectorySize(string path)
        {
            long result = 0;
            foreach (var i in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                FileInfo info = new FileInfo(i);
                result += info.Length;
            }

            return result;
        }



        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressWindow.Close();
            Visible = true;

            if (e.Error != null)
            {
                MessageBox.Show(this, $"An unexpected error occured: {e.Error}", "Error", MessageBoxButtons.OK);
            }
            else
            {
                FolderMoveResult result = (FolderMoveResult)e.Result;

                if (result.HasFlag(enumFolderMoveResult.CleanDestination))
                {
                    Directory.Delete(txbDestination.Text, true);
                    Directory.CreateDirectory(txbDestination.Text);
                }
                else if (result.HasFlag(enumFolderMoveResult.CleanDestination))
                {
                    Directory.Delete(txbDestination.Text, true);
                }

                if (result.HasFlag(enumFolderMoveResult.DeleteLink))
                {
                    File.Delete(txbSource.Text);
                }

                if (result.HasFlag(enumFolderMoveResult.RevertMoveSource))
                {
                    Directory.Move(Path.Combine(Path.GetTempPath(), Path.GetFileName(txbSource.Text) + ".old"), txbSource.Text);
                }


                if (e.Cancelled)
                {
                    MessageBox.Show(this, $"Operation canceled by user.", "Canceled", MessageBoxButtons.OK);
                }
                else if (!result.IsFlag(enumFolderMoveResult.Success))
                {
                    MessageBox.Show(this, $"Folder\n\t{txbSource.Text}\ncould not be copyed.", "Failed", MessageBoxButtons.OK);
                }
                else
                {
                    MessageBox.Show(this, $"Folder\n\t{txbSource.Text}\nis copyed to\n\t{txbDestination.Text}", "Success", MessageBoxButtons.OK);
                }
            }
        }

        private void btnSourceBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Source Folder";
            dialog.SelectedPath = txbSource.Text;

            DialogResult result = dialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                txbSource.Text = dialog.SelectedPath;

                if (txbDestination.Text.Length > 0)
                    txbDestination.Text = Path.Combine(Path.GetDirectoryName(txbDestination.Text), Path.GetFileName(txbSource.Text));
            }
        }

        private void btnDestinationBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Destination Folder";
            dialog.SelectedPath = txbDestination.Text;

            DialogResult result = dialog.ShowDialog(this);
            if (result == DialogResult.OK)
                txbDestination.Text = dialog.SelectedPath + Path.GetFileName(txbSource.Text);
        }

        private string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "settings.xml");

        private void frmMain_Load(object sender, EventArgs e)
        {
            Top = Math.Max(0, Cursor.Position.Y - Height / 2);
            Left = Math.Max(0, Cursor.Position.X - Width / 2);

            if (ApplicationDeployment.IsNetworkDeployed)
                lblVersion.Text = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            else
            {
                lblVersion.Text = $"{Application.ProductVersion} (debug)";
            }

            if (File.Exists(SettingsPath))
            {
                XmlDocument xml = new XmlDocument();

                try
                {
                    xml.Load(SettingsPath);
                }
                catch
                {
                    return;
                }

                XmlNode node;

                node = xml.SelectSingleNode("/*[local-name()='settings']/*[local-name()='source']");
                txbSource.Text = node?.InnerText;

                node = xml.SelectSingleNode("/*[local-name()='settings']/*[local-name()='destination']");
                if (node != null && node.InnerText.Length != 0 && txbSource.Text.Length != 0)
                    txbDestination.Text = Path.Combine(node.InnerText, Path.GetFileName(txbSource.Text));
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            using (XmlWriter writer = XmlWriter.Create(SettingsPath, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
            {
                writer.WriteStartElement("settings");
                writer.WriteElementString("source", txbSource.Text);
                if (string.IsNullOrEmpty(txbDestination.Text))
                    writer.WriteElementString("destination", string.Empty);
                else
                    writer.WriteElementString("destination", Path.GetDirectoryName(txbDestination.Text));
                writer.WriteEndElement();
            }
        }
    }
}
