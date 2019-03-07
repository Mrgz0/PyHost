using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PyHost
{
    public partial class MainForm : Form
    {
        private Thread thread;
        private bool threadNeedPause = false;
        private ContextMenuStrip lvMenuStrip = new ContextMenuStrip();
        public MainForm()
        {
            InitializeComponent();
            PyScript.PyOutPut += new PyScript.PyOutPutHandler(PyOutPut);
        }
        private void timerEventProcessor(object source, EventArgs e)
        {
            this.lvPy.Clear();//清空全部,包括标题
            this.lvPy.Columns.Add("Id", 0, HorizontalAlignment.Center);
            this.lvPy.Columns.Add("Name", 100, HorizontalAlignment.Center);
            this.lvPy.Columns.Add("?", 20, HorizontalAlignment.Center);
            this.lvPy.Columns.Add("Path", 65, HorizontalAlignment.Center);
            this.lvPy.Items.Clear();//清空内容 

            foreach (var py in Common.Repository.GetAllPy())
            {
                ListViewItem item = new ListViewItem(new string[] { py.Id.ToString(), py.Name, py.IsRunning ? "T" : "F", py.Path });
                if (!py.IsRunning && !py.CloseFinded)
                {
                    py.CloseFind();
                }
                this.lvPy.Items.Add(item);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshLV();

            var eh = new System.EventHandler(this.LvMenuItemClick);
            lvMenuStrip.Items.Add("Del", null, eh);


            this.thread = new Thread(new System.Threading.ThreadStart(() =>
            {
                int count = 0;
                while (true)
                {
                    if (!this.threadNeedPause)
                    {
                        count++;
                        foreach (var py in Common.Repository.GetAllPy())
                        {
                            if (!py.IsRunning && !py.CloseFinded)
                            {
                                py.CloseFind();
                            }
                        }
                        if (count > 2)
                        {
                            count = 0;

                            this.lvPy.Invoke(new MethodInvoker(() =>
                            {
                                this.UpdateLV();
                           }));
                        }
                    }


                    Thread.Sleep(1000);
                }

            }));
            this.thread.IsBackground = true;
            this.thread.Start();
        }
        private void LvMenuItemClick(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (this.CurrentPy == null) return;
            Common.Repository.DelPy(this.CurrentPy.Id);
            this.RefreshLV();
        }

        public void RefreshLV()
        {
            int oldId = -1;
            if (this.lvPy.SelectedItems.Count > 0)
            {
                oldId = Int32.Parse(this.lvPy.SelectedItems[0].SubItems[0].Text);
            }
            this.lvPy.Clear();//清空全部,包括标题
            this.lvPy.Columns.Add("Id", 0, HorizontalAlignment.Left);
            this.lvPy.Columns.Add("Name", 100, HorizontalAlignment.Left);
            this.lvPy.Columns.Add("?", 20, HorizontalAlignment.Left);
            this.lvPy.Columns.Add("Path", 65, HorizontalAlignment.Left);
            this.lvPy.Items.Clear();//清空内容 

            foreach (var py in Common.Repository.GetAllPy())
            {
                ListViewItem item = new ListViewItem(new string[] { py.Id.ToString(), py.Name, py.IsRunning ? "T" : "F", py.Path });

                this.lvPy.Items.Add(item);
                if (py.Id == oldId)
                {
                    item.Selected = true;
                }
            }
        }

        public void UpdateLV()
        {
            foreach (ListViewItem item in this.lvPy.Items)
            {
                int id = Int32.Parse(item.SubItems[0].Text);
                var py = Common.Repository.GetPy(id);
                if (py != null)
                {
                    item.SubItems[2].Text = py.IsRunning ? "T" : "F";
                }
            }
        }

        public void PyOutPut(int pyId, PyResultItem item)
        {
            this.tbResult.Invoke(new MethodInvoker(() =>
            {
                if (this.CurrentPy != null && this.CurrentPy.Id == pyId)
                {
                    tbResult.Select(tbResult.TextLength, 0);
                    tbResult.ScrollToCaret();

                    if (!item.IsNormal)
                    {
                        string text = item.Text + "\n";
                        tbResult.SelectionColor = Color.Red;
                        tbResult.AppendText(text);
                        tbResult.SelectionColor = tbResult.ForeColor;
                    }
                    else
                    {
                        string text = item.Text + "\n";
                        tbResult.AppendText(text);
                    }

                    tbResult.Select(tbResult.TextLength, 0);
                    tbResult.ScrollToCaret();
                }
            }
            ));
        }

        #region 基本状态信息

        public PyScript CurrentPy
        {
            get
            {
                if (this.lvPy.SelectedItems.Count == 1)
                {
                    return Common.Repository.GetPy(Int32.Parse(this.lvPy.SelectedItems[0].SubItems[0].Text));
                }
                return null;
            }
        }
        #endregion

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (this.CurrentPy != null)
            {
                this.CurrentPy.CloseProc();
                this.UpdateLV();
            }
        }
        private void btnRun_Click(object sender, EventArgs e)
        {
            if (this.CurrentPy != null)
            {
                if (this.CurrentPy.IsRunning)
                {
                    MessageBox.Show("Already running");
                    return;
                }
                bool b = this.CurrentPy.StartProc();
                if (!b)
                {
                    MessageBox.Show("Start fail");
                }
                else
                {
                    this.UpdateLV();
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            this.threadNeedPause = true;
            AddDlg dlg = new AddDlg();
            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                RefreshLV();
            }
            this.threadNeedPause = false;
        }

        private void lvPy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.CurrentPy != null)
            {
                this.tbResult.Invoke(new MethodInvoker(() =>
                {
                    this.tbResult.Clear();
                    foreach (var item in this.CurrentPy.PyResultArray)
                    {

                        if (!item.IsNormal)
                        {
                            string text = item.Text + "\n";
                            tbResult.SelectionColor = Color.Red;
                            tbResult.AppendText(text);
                            tbResult.SelectionColor = tbResult.ForeColor;
                        }
                        else
                        {
                            string text = item.Text + "\n";
                            tbResult.AppendText(text);

                        }
                    }
                    tbResult.Select(tbResult.TextLength, 0);
                    tbResult.ScrollToCaret();
                }));

            }
            else
            {
                this.tbResult.Invoke(new MethodInvoker(() =>
                {
                    this.tbResult.Clear();
                }));

            }
        }

        private void msLVRightPop_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void lvPy_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                lvMenuStrip.Show(this.lvPy, e.Location);//鼠标右键按下弹出菜单
            }
        }

        private void lvPy_MouseDoubleClick(object sender, MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Left)
            {
                this.threadNeedPause = true;

                AddDlg dlg = new AddDlg();
                dlg.Text = "Edit";

                if (this.CurrentPy != null)
                {
                    dlg.EditId = this.CurrentPy.Id;
                    dlg.EditName = this.CurrentPy.Name;
                    dlg.EditPath = this.CurrentPy.Path;
                }

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    this.RefreshLV();
                }
                this.threadNeedPause = false;
            }

            if (this.CurrentPy != null)
            {
                this.tbResult.Invoke(new MethodInvoker(() =>
                {
                    this.tbResult.Clear();
                    foreach (var item in this.CurrentPy.PyResultArray)
                    {

                        if (!item.IsNormal)
                        {
                            string text = item.Text + "\n";
                            tbResult.SelectionColor = Color.Red;
                            tbResult.AppendText(text);
                            tbResult.SelectionColor = tbResult.ForeColor;

                        }
                        else
                        {
                            string text = item.Text + "\n";
                            tbResult.AppendText(text);

                        }
                    }
                    tbResult.Select(tbResult.TextLength, 0);
                    tbResult.ScrollToCaret();
                }));

            }
            else
            {
                this.tbResult.Invoke(new MethodInvoker(() =>
                {
                    this.tbResult.Clear();
                }));

            }
        }



        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (var id in Common.Repository.GetAllProcId())
            {
                Common.KillProcessAndChildren(id);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show(
            "注意：窗口关闭将结束所有脚本",
            "提示",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question) != DialogResult.OK)
            {
                e.Cancel = true;
                return;
            }
        }

        private void tbResult_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnClear_Click(object sender, EventArgs e)
        {

        }
    }
}
