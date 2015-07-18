using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class BruteforceForm : Form
    {
        bool running;
        AsyncAction bgAction;

        public BruteforceForm()
        {
            InitializeComponent();

            bgAction = new AsyncAction(() => BruteForce());
            bgAction.ProgressChanged += A_ProgressChanged;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            try
            {
                running = !running;

                if (running)
                {
                    await bgAction.DoAction();
                    MessageBox.Show("Done!");
                }
                else
                {
                    bgAction.Cancel();
                }
            }
            catch (AggregateException)
            {
                MessageBox.Show("Cancelled!");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Cancelled!");
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error during bruteforce:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void A_ProgressChanged(object sender, AsyncActionProgressChangedEventArgs e)
        {
            progressBar1.Value = e.Progress;

            if (e.UserData != null)
                label1.Text = (string)e.UserData;
        }

        private void BruteForce()
        {
            PassEnumerator e = new PassEnumerator("abcdefghijklmnopqrstuvwxyz", 7);

            IEnumerable<string> variations = e.Enumerate();

            Parallel.ForEach(variations, (s) =>
            {
                bgAction.ThrowOnCancel();

                int pct = (int)((double)e.Processed / (double)e.TotalCount * 100.0f);

                if (e.Processed % 2000 == 0)
                {
                    bgAction.ReportProgress(pct, s);
                }
            });

            //foreach (var s in variations)
            //{
            //    bgAction.ThrowOnCancel();

            //    int pct = (int)((double)e.Processed / (double)e.TotalCount * 100.0f);

            //    if (e.Processed % 2000 == 0)
            //    {
            //        bgAction.ReportProgress(pct, s);
            //    }
            //}
        }
    }
}
