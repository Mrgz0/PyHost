using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml;

namespace PyHost
{
    public class PyResultItem
    {
        public string Text { get; set; }
        public bool IsNormal { get; set; }
    }
    public class PyScript
    {
        public const int MaxRecordCount = 500;
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public bool IsRunning
        {
            get
            {
                try
                {
                    if (this._Proc != null && !this._Proc.HasExited)
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {

                }
                return false;
            }
        }

        public bool _CloseFinded = true;
        public bool CloseFinded { get { return this._CloseFinded; } set { this._CloseFinded = value; } }

        private System.Diagnostics.Process _Proc;
        public System.Diagnostics.Process Proc { get { return this._Proc; } set { this._Proc = value; } }

        private object queue_lock = new object();
        private Queue<PyResultItem> _PyResultQueue = new Queue<PyResultItem>();
        public PyResultItem[] PyResultArray
        {
            get
            {
                lock (queue_lock)
                { return this._PyResultQueue.ToArray(); }
            }
        }

        private void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            PyResultItem item = new PyResultItem() { Text = e.Data, IsNormal = false };
            lock (queue_lock)
            {
                this._PyResultQueue.Enqueue(item);
            }
            if (PyOutPut != null) PyOutPut(this.Id, item);

            for (int i = 0; i < this._PyResultQueue.Count - MaxRecordCount; i++)
            {
                lock (queue_lock)
                {
                    this._PyResultQueue.Dequeue();
                }
            }
        }

        private void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            PyResultItem item = new PyResultItem() { Text = e.Data, IsNormal = true };

            lock (queue_lock)
            {
                this._PyResultQueue.Enqueue(item);
            }
            if (PyOutPut != null) PyOutPut(this.Id, item);

            for (int i = 0; i < this._PyResultQueue.Count - MaxRecordCount; i++)
            {

                lock (queue_lock)
                {
                    this._PyResultQueue.Dequeue();
                }
            }
        }

        private void p_Exited(object sender, EventArgs e)
        {
            PyResultItem item = new PyResultItem() { Text = "进程已结束", IsNormal = true };

            lock (queue_lock)
            {
                this._PyResultQueue.Enqueue(item);
            }
            if (PyOutPut != null) PyOutPut(this.Id, item);
        }

        public delegate void PyOutPutHandler(int id, PyResultItem item);

        public static event PyOutPutHandler PyOutPut = null;
        public bool StartProc()
        {
            if (this.IsRunning)
            {
                return false;
            }

            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();

                process.StartInfo.FileName = this.Path;
                try
                {
                    process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(this.Path);
                }
                catch
                {

                }
                // 必须禁用操作系统外壳程序
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardInput = false;

                // 为异步获取订阅事件  
                process.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);
                process.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);


                //process.Exited += new EventHandler(p_Exited);
                process.Start();

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                this.CloseFinded = false;
                this._Proc = process;
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public bool CloseFind()
        {
            this.CloseFinded = true;
            PyResultItem item = new PyResultItem() { Text = "进程已结束", IsNormal = true };

            lock (queue_lock)
            {
                this._PyResultQueue.Enqueue(item);
            }
            if (PyOutPut != null) PyOutPut(this.Id, item);
            return true;
        }
        public bool CloseProc()
        {
            if (this.IsRunning)
            {
                Common.KillProcessAndChildren(this._Proc.Id);
                this._Proc.Dispose();
            }
            return true;
        }
    }

    public class Repository
    {
        object dic_lock = new object();
        Dictionary<int, PyScript> PyDic = new Dictionary<int, PyScript>();

        string FilePath = "./scripts.xml";

        public Repository()
        {
            this.LoadFromFile();
        }

        public bool SaveToFile()
        {
            try
            {
                DataTable dt = new DataTable("Data");
                dt.Columns.Add("Name", typeof(System.String));
                dt.Columns.Add("Path", typeof(System.String));

                lock(dic_lock)
                {
                    foreach(var py in PyDic.Values)
                    {
                        var row = dt.NewRow();
                        row["Name"] = py.Name;
                        row["Path"] = py.Path;

                        dt.Rows.Add(row);
                    }
                }

                dt.WriteXml(FilePath, XmlWriteMode.WriteSchema);
                dt.Dispose();
            }
            catch(Exception e)
            {
                return false;
            }
            return true;
        }
        public bool LoadFromFile()
        {

            DataTable dt = new DataTable();
            try
            {
                dt.ReadXml(FilePath);
                foreach(DataRow dr in dt.Rows)
                {
                    string name = dr["Name"].ToString();
                    string path = dr["Path"].ToString();
                    this.AddPy(name,path);
                }
                return true;
            }
            catch
            {
            }
            return false;

        }
        public bool AddPy(string name, string path)
        {
            lock (dic_lock)
            {
                int maxId = 0;
                if (PyDic.Keys.Count > 0)
                {
                    if (PyDic.Values.Select(t => t.Name.ToLower()).Contains(name.ToLower()))
                        return false;
                    maxId = PyDic.Keys.Max();

                }
                var py = new PyScript();
                py.Id = maxId + 1;
                py.Name = name;
                py.Path = path;

                PyDic.Add(py.Id, py);
            }

            return true;
        }
        public bool UpdatePy(int id, string name, string path)
        {
            lock (dic_lock)
            {
                if (!PyDic.TryGetValue(id, out PyScript py))
                {
                    return false;
                }

                py.Name = name;
                py.Path = path;
            }
            return true;
        }
        public bool DelPy(int id)
        {
            lock (dic_lock)
            {
                PyDic.Remove(id);
            }
            return true;
        }

        public int[] GetAllProcId()
        {

            List<int> idList = new List<int>();
            lock (dic_lock)
            {
                foreach (var py in PyDic.Values)
                {
                    try
                    {
                        if (py.Proc != null)
                            idList.Add(py.Proc.Id);
                    }
                    catch
                    { }
                }
            }
            return idList.ToArray();
        }
        public PyScript[] GetAllPy()
        {
            lock (dic_lock)
            {
                return PyDic.Values.ToArray();
            }
        }

        public PyScript GetPy(int id)
        {
            lock (dic_lock)
            {
                bool b = PyDic.TryGetValue(id, out PyScript py);
                if (b)
                {
                    return py;
                }
            }
            return null;
        }
    }
    public static class Common
    {
        private static Repository _Repository = new Repository();

        public static Repository Repository { get { return _Repository; } }

        static Common()
        {
            _Repository = new Repository();
        }

        public static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (Exception)
            {
                /* process already exited */
            }
        }
    }

}
