using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace CryptoGatherer
{
    public partial class Form1 : Form
    {
        private static readonly HashAlgorithm sha512 = SHA512.Create();
        public Form1()
        {
            InitializeComponent();
            this.fileListView.ListViewItemSorter = columnSorter;
        }

        private ListViewColumnSorter columnSorter = new ListViewColumnSorter();

        public static string CreateHash(string input)
        {
            return HttpServerUtility.UrlTokenEncode(sha512.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (
                string.IsNullOrWhiteSpace(this.language.Text) ||
                string.IsNullOrWhiteSpace(this.fileContents.Text))
            {
                MessageBox.Show("Ensure that Language and FileContents are set.");
                return;
            }
            var realFilename = Path.Combine("CryptoPatterns",$"crypto-patterns-{CreateHash(fileContents.Text)}.txt");
            var codeSnippet = new CodeSnippet(
                1, 
                fileName.Text, 
                sourceUrl.Text, 
                packageName.Text, 
                (CodeLanguage)Enum.Parse(typeof(CodeLanguage), language.Text),
                algorithms.SelectedItems.Cast<string>().Select(x => (CryptoAlgorithm)Enum.Parse(typeof(CryptoAlgorithm),x)).ToArray(), 
                isFullFile.Checked, 
                fileContents.Text.Trim());

            File.WriteAllText(realFilename, codeSnippet.ToString());
            this.fileName.Text = "";
            this.fileContents.Text = "";
            this.algorithms.SelectedItems.Clear();
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

            var lines = File.ReadAllText(filename);
            var result = CodeSnippet.FromString(lines);
            if (result is null)
            {
                MessageBox.Show(string.Format("Error loading {0}: Could not parse this as a CodeSnippet.", filename), "Error", MessageBoxButtons.OK);
                return;
            }

            fileName.Text = result.name;
            sourceUrl.Text = result.sourceUrl;
            packageName.Text = result.packageName;
            language.Text = result.language.ToString();
            algorithms.SelectedItems.Clear();
            foreach (var alg in result.algorithms)
            {
                algorithms.SelectedItems.Add(alg.ToString());
            }
            isFullFile.Checked = result.isFullFile;
            fileContents.Text = result.content;
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