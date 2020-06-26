using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CryptoGatherer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.fileListView.ListViewItemSorter = columnSorter;
        }

        private ListViewColumnSorter columnSorter = new ListViewColumnSorter();

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.fileName.Text))
            {
                this.fileName.Text = Guid.NewGuid().ToString().Substring(0, 8);
            }

            if (string.IsNullOrWhiteSpace(this.fileName.Text) ||
                string.IsNullOrWhiteSpace(this.language.Text) ||
                this.algorithms.SelectedItems.Count == 0 ||
                string.IsNullOrWhiteSpace(this.fileContents.Text))
            {
                MessageBox.Show("Error");
                return;
            }

            var realFilename = Path.Combine("CryptoPatterns",$"crypto-patterns-{this.fileName.Text}.txt");
            var sb = new StringBuilder();

            sb.AppendLine("version=1");
            sb.AppendLine(this.fileName.Text);
            sb.AppendLine(this.sourceUrl.Text);
            sb.AppendLine(this.packageName.Text);
            sb.AppendLine(this.language.Text);
            sb.AppendLine(string.Join(",", this.algorithms.SelectedItems.Cast<string>()));
            sb.AppendLine(this.isFullFile.Checked ? "checked=true" : "checked=false");
            sb.AppendLine("--");
            sb.AppendLine(this.fileContents.Text.Trim());

            File.WriteAllText(realFilename, sb.ToString());
            RefreshData();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.fileName.Text = "";
            this.sourceUrl.Text = "";
            this.packageName.Text = "";
            this.language.Text = "";
            this.algorithms.SelectedItems.Clear();
            this.isFullFile.Checked = false;
            this.fileContents.Text = "";
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void fileListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var indices = fileListView.SelectedItems;
            if (indices.Count == 0)
            {
                return;
            }
            string filename = "";
            foreach (ListViewItem index in indices)
            {
                filename = index.Text;
                break;
            }
            if (filename == "")
            {
                MessageBox.Show("Weird.");
            }
            try
            {
                var lines = File.ReadAllText(filename).Split(new char[] { '\n' });
                if (!lines[0].Trim().Equals("version=1"))
                {
                    MessageBox.Show("Error, wrong version.");
                    return;
                }
                this.fileName.Text = lines[1].Trim();
                this.sourceUrl.Text = lines[2].Trim();
                this.packageName.Text = lines[3].Trim();
                this.language.Text = lines[4].Trim();
                this.algorithms.SelectedItems.Clear();
                foreach (var alg in lines[5].Trim().Split(new[] { ',' }))
                {
                    this.algorithms.SelectedItems.Add(alg);
                }
                this.isFullFile.Checked = lines[6].Trim().Contains("checked=true");
                this.fileContents.Text = string.Join("\n", lines.Skip(8));
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error loading {0}: {1}", filename, ex.Message), "Error", MessageBoxButtons.OK);
            }
        }

        private void fileListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == columnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (columnSorter.Order == SortOrder.Ascending)
                {
                    columnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    columnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                columnSorter.SortColumn = e.Column;
                columnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.fileListView.Sort();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RefreshData();
        }

        private void label3_Click(object sender, EventArgs e)
        {
        }

        private void RefreshData()
        {
            fileListView.Items.Clear();
            fileListView.BeginUpdate();
            foreach (var filename in Directory.EnumerateFiles("CryptoPatterns", "crypto-patterns-*.txt", SearchOption.TopDirectoryOnly))
            {
                var _packageName = "";
                var _algorithms = "";

                try
                {
                    var lines = File.ReadAllText(filename).Split(new char[] { '\n' });
                    if (lines[0].Trim().Equals("version=1"))
                    {
                        _packageName = lines[4].Trim();
                        _algorithms = lines[5].Trim();
                    }
                }
                catch (Exception ex)
                {
                }

                var row = new ListViewItem(filename);
                row.SubItems.Add(_packageName);
                row.SubItems.Add(_algorithms);

                fileListView.Items.AddRange(new ListViewItem[] { row });
            }
            fileListView.EndUpdate();
            fileListView.Refresh();
        }
    }
}