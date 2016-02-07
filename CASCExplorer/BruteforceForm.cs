using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class BruteforceForm : Form
    {
        bool running;

        public BruteforceForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!running)
            {
                if (!backgroundWorker1.IsBusy)
                {
                    backgroundWorker1.RunWorkerAsync();
                    button1.Text = "Stop";
                    running = true;
                }
            }
            else
            {
                backgroundWorker1.CancelAsync();
                button1.Text = "Start";
                running = false;
            }
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            PassEnumerator en = new PassEnumerator("abcdefghijklmnopqrstuvwxyz", 7);

            IEnumerable<string> variations = en.Enumerate();

            Parallel.ForEach(variations, (val, ps) =>
            {
                if (backgroundWorker1.CancellationPending)
                {
                    ps.Stop();
                    return;
                }

                int pct = (int)(en.Processed / (double)en.TotalCount * 100.0f);

                if (en.Processed % 2000 == 0)
                {
                    backgroundWorker1.ReportProgress(pct, val);
                }
            });
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Done!");
        }

        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;

            if (e.UserState != null)
                label1.Text = (string)e.UserState;
        }

        private void BruteforceForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            backgroundWorker1.CancelAsync();
        }
    }
}
