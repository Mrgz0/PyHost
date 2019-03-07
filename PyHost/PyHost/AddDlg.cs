using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PyHost
{
    public partial class AddDlg : Form
    {
        public int? EditId { get; set; }
        public string EditName { get; set; }
        public  string EditPath { get; set; }
        public AddDlg()
        {
            InitializeComponent();
        }

        private void btnBrowser_Click(object sender, EventArgs e)
        {
            OpenFileDialog file = new OpenFileDialog();
            file.ShowDialog();
            this.tbPath.Text = file.FileName;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.tbName.Text) || string.IsNullOrEmpty(this.tbPath.Text))
            {
                MessageBox.Show("Empty");
            }
            else
            {
                bool b;
                if (this.EditId.HasValue)
                {
                    b = Common.Repository.UpdatePy(this.EditId.Value, this.tbName.Text, this.tbPath.Text);
                }
                else
                {
                    b = Common.Repository.AddPy(this.tbName.Text, this.tbPath.Text);
                }
                if (b)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Fail");
                }
                Common.Repository.SaveToFile();
            }
            
        }

        private void AddDlg_Load(object sender, EventArgs e)
        {
            if (this.EditId.HasValue)
            {
                this.tbName.Text = this.EditName;
                this.tbPath.Text = this.EditPath;
            }
        }
    }
}
