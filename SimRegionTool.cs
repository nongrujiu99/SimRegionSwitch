using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SimRegionTool
{
    /// <summary>
    /// SIM Region Switch 一键工具（PC 端，免 Shizuku）。
    /// 原理：电脑 adb 本身即 shell 身份，可直接启动本 APK 的 CarrierOverrideInstrumentation
    /// 完成 SIM 国家码覆盖 / 恢复，无需安装 Shizuku、无需配对。
    /// 行为基线以 SimRegion一键工具.exe 为准：改动源码后请运行 build_exe.bat 重新编译并验证。
    /// </summary>
    public class MainForm : Form
    {
        // ======================= 常量 =======================
        private const string PKG = "com.example.simregionswitch";
        private const string INSTR = "com.example.simregionswitch/.CarrierOverrideInstrumentation";
        private const string APK_NAME = "SimRegionSwitch-debug.apk";
        private const string EMBED_DIR = "srs_embedded";

        private const int ADB_TIMEOUT_DEVICES = 15000;   // adb devices 超时
        private const int ADB_TIMEOUT_DUMPSYS = 30000;   // dumpsys 读取超时
        private const int ADB_TIMEOUT_OPERATION = 120000; // 安装 / instrumentation 超时
        private const int REFRESH_INTERVAL_MS = 3000;    // 状态自动刷新间隔
        private const int EMBED_BUFFER_SIZE = 65536;     // 嵌入资源写出缓冲

        // ======================= 控件字段 =======================
        private Label lblConn;
        private Label lblNext;
        private Label lblCurrent;
        private Label lblSubId;
        private ComboBox cmbCountry;
        private TextBox txtCustom;
        private Label lblResult;
        private Button btnApply;
        private Button btnRestore;
        private RichTextBox txtLog;
        private System.Windows.Forms.Timer refreshTimer;
        private bool busy;
        private bool refreshRunning;
        private BackgroundWorker worker;

        public MainForm()
        {
            BuildUi();
            InitWorker();
            refreshTimer = new System.Windows.Forms.Timer { Interval = REFRESH_INTERVAL_MS };
            refreshTimer.Tick += RefreshTick;
            refreshTimer.Start();
            RefreshTick(null, EventArgs.Empty);
        }

        // ======================= UI 构建 =======================
        private void BuildUi()
        {
            Text = "SIM Region Switch 一键工具";
            ClientSize = new Size(500, 700);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.White;

            // 顶部连接状态卡片
            var card = new Panel { Bounds = new Rectangle(10, 10, 480, 84), BackColor = Color.FromArgb(240, 246, 255) };
            lblConn = new Label { Bounds = new Rectangle(16, 12, 448, 30), Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), Text = "正在检测...", ForeColor = Color.FromArgb(64, 64, 64) };
            lblNext = new Label { Bounds = new Rectangle(16, 48, 448, 32), Font = new Font("Microsoft YaHei UI", 9F), Text = "请用数据线连接手机并开启 USB 调试", ForeColor = Color.FromArgb(30, 120, 230) };
            card.Controls.Add(lblConn);
            card.Controls.Add(lblNext);

            // 当前 SIM 信息
            lblCurrent = new Label { Bounds = new Rectangle(10, 102, 480, 22), Font = new Font("Microsoft YaHei UI", 10F), Text = "当前 SIM 国家码：--" };
            lblSubId = new Label { Bounds = new Rectangle(10, 126, 480, 22), Font = new Font("Microsoft YaHei UI", 9F), ForeColor = Color.FromArgb(90, 90, 90), Text = "SIM 信息：--" };

            // 目标地区选择
            var lblCountry = new Label { Bounds = new Rectangle(10, 158, 86, 24), Text = "目标地区：" };
            cmbCountry = new ComboBox { Bounds = new Rectangle(96, 156, 190, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCountry.Items.Add("美国 US");
            cmbCountry.Items.Add("日本 JP");
            cmbCountry.Items.Add("新加坡 SG");
            cmbCountry.Items.Add("香港 HK");
            cmbCountry.Items.Add("英国 GB");
            cmbCountry.Items.Add("加拿大 CA");
            cmbCountry.Items.Add("自定义...");
            cmbCountry.SelectedIndex = 0;
            cmbCountry.SelectedIndexChanged += delegate { txtCustom.Visible = cmbCountry.SelectedIndex == cmbCountry.Items.Count - 1; };
            txtCustom = new TextBox { Bounds = new Rectangle(292, 157, 100, 24), Visible = false };

            // 结果提示
            lblResult = new Label { Bounds = new Rectangle(10, 194, 480, 62), Font = new Font("Microsoft YaHei UI", 9.5F), BackColor = Color.FromArgb(245, 245, 245), Text = "就绪", ForeColor = Color.FromArgb(80, 80, 80), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8) };

            // 操作按钮
            btnApply = new Button { Bounds = new Rectangle(10, 266, 480, 48), Text = "一键应用", Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), BackColor = Color.FromArgb(48, 130, 240), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += delegate { StartApply(); };

            btnRestore = new Button { Bounds = new Rectangle(378, 322, 112, 28), Text = "恢复原始配置", Font = new Font("Microsoft YaHei UI", 9F), BackColor = Color.FromArgb(240, 240, 240), ForeColor = Color.FromArgb(150, 60, 60), FlatStyle = FlatStyle.Flat };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += delegate { StartRestore(); };

            // 日志区
            txtLog = new RichTextBox { Bounds = new Rectangle(10, 360, 480, 330), ReadOnly = true, Font = new Font("Consolas", 9F), BackColor = Color.FromArgb(250, 250, 250), BorderStyle = BorderStyle.FixedSingle };

            Controls.Add(card);
            Controls.Add(lblCurrent);
            Controls.Add(lblSubId);
            Controls.Add(lblCountry);
            Controls.Add(cmbCountry);
            Controls.Add(txtCustom);
            Controls.Add(lblResult);
            Controls.Add(btnApply);
            Controls.Add(btnRestore);
            Controls.Add(txtLog);
        }

        // ======================= 数据类 =======================
        private class CmdResult { public int Exit; public string Out; }
        private class DeviceInfo { public string State; public string Line; }
        private class Snapshot { public string Adb; public string DevState; public int SubId; public string Country; }
        private class StepResult { public bool Ok; public string Title; public string Advice; public string Country; }

        // ======================= 嵌入资源 =======================
        // adb.exe / AdbWinApi.dll / AdbWinUsbApi.dll / APK 均内嵌于 EXE，
        // 首次运行时解压到 %TEMP%\srs_embedded，之后按需复用。

        private static void EnsureEmbeddedResources()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), EMBED_DIR);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                ExtractResource("adb.exe", dir);
                ExtractResource("AdbWinApi.dll", dir);
                ExtractResource("AdbWinUsbApi.dll", dir);
                ExtractResource(APK_NAME, dir);
            }
            catch { }
        }

        private static void ExtractResource(string name, string dir)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return;
                string dest = Path.Combine(dir, name);
                if (File.Exists(dest))
                {
                    var fi = new FileInfo(dest);
                    if (fi.Length == s.Length) return; // 已存在且大小一致则跳过
                }
                using (var fs = File.Create(dest))
                {
                    byte[] buf = new byte[EMBED_BUFFER_SIZE];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, n);
                }
            }
        }

        private static string EmbeddedPath(string name)
        {
            return Path.Combine(Path.GetTempPath(), EMBED_DIR, name);
        }

        // ======================= adb 基础工具 =======================
        private static string FindAdb()
        {
            // 查找顺序：内嵌解压 → 程序目录 adb\adb.exe → PATH
            string embedded = EmbeddedPath("adb.exe");
            if (File.Exists(embedded)) return embedded;
            string local = Path.Combine(AppContext.BaseDirectory, "adb", "adb.exe");
            if (File.Exists(local)) return local;
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(';'))
            {
                try
                {
                    string f = Path.Combine(dir.Trim('"'), "adb.exe");
                    if (File.Exists(f)) return f;
                }
                catch { }
            }
            return null;
        }

        private static CmdResult RunAdb(string adb, string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo(adb, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            using (var p = Process.Start(psi))
            {
                // 并行读 stdout / stderr，避免管道阻塞导致死锁
                var sb = new StringBuilder();
                var t1 = new Thread(delegate() { try { sb.Append(p.StandardOutput.ReadToEnd()); } catch { } });
                var t2 = new Thread(delegate() { try { sb.Append("\n").Append(p.StandardError.ReadToEnd()); } catch { } });
                t1.IsBackground = true;
                t2.IsBackground = true;
                t1.Start();
                t2.Start();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                }
                t1.Join(3000);
                t2.Join(3000);
                var r = new CmdResult();
                r.Exit = p.ExitCode;
                r.Out = sb.ToString().Trim();
                return r;
            }
        }

        private static DeviceInfo GetDevice(string adb)
        {
            var r = RunAdb(adb, "devices", ADB_TIMEOUT_DEVICES);
            var d = new DeviceInfo();
            d.State = "none";
            d.Line = "";
            foreach (string line in r.Out.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("List of") || t.StartsWith("*")) continue;
                string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    d.State = parts[1].ToLowerInvariant();
                    d.Line = t;
                    break;
                }
            }
            return d;
        }

        private static int GetSubId(string adb)
        {
            // 优先 dumpsys isub 的 subId，失败再回退 telephony.registry 的 mActiveDataSubId
            var r = RunAdb(adb, "shell dumpsys isub", ADB_TIMEOUT_DUMPSYS);
            var m = Regex.Match(r.Out, @"subId\s*[:=]\s*(\d+)");
            if (m.Success)
            {
                int v;
                if (int.TryParse(m.Groups[1].Value, out v)) return v;
            }
            r = RunAdb(adb, "shell dumpsys telephony.registry", ADB_TIMEOUT_DUMPSYS);
            m = Regex.Match(r.Out, @"mActiveDataSubId\s*=\s*(\d+)");
            if (m.Success)
            {
                int v;
                if (int.TryParse(m.Groups[1].Value, out v)) return v;
            }
            return -1;
        }

        private static string GetCountry(string adb)
        {
            var r = RunAdb(adb, "shell dumpsys telephony.registry", ADB_TIMEOUT_DUMPSYS);
            var m = Regex.Match(r.Out, @"mSimCountryIso\s*=\s*(\S+)");
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            m = Regex.Match(r.Out, @"mCountryIso\s*=\s*(\S+)");
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            r = RunAdb(adb, "shell dumpsys isub", ADB_TIMEOUT_DUMPSYS);
            m = Regex.Match(r.Out, @"countryIso=(\S+)");
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            return "未知";
        }

        private string ApkPath()
        {
            string embedded = EmbeddedPath(APK_NAME);
            if (File.Exists(embedded)) return embedded;
            return Path.Combine(AppContext.BaseDirectory, APK_NAME);
        }

        private string SelectedCountry()
        {
            int idx = cmbCountry.SelectedIndex;
            switch (idx)
            {
                case 0: return "US";
                case 1: return "JP";
                case 2: return "SG";
                case 3: return "HK";
                case 4: return "GB";
                case 5: return "CA";
                default:
                    string t = txtCustom.Text;
                    return (t == null ? "" : t.Trim().ToUpperInvariant());
            }
        }

        // ======================= UI 辅助 =======================
        private void AppendLog(string line)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\n");
            txtLog.ScrollToCaret();
        }

        private void SetConn(string text, Color color)
        {
            lblConn.Text = text;
            lblConn.ForeColor = color;
        }

        private void SetNext(string text)
        {
            lblNext.Text = text;
        }

        private void SetResult(bool ok, string title, string advice)
        {
            lblResult.Text = (ok ? "✓ " : "✗ ") + title + "\n" + advice;
            lblResult.ForeColor = ok ? Color.FromArgb(20, 130, 60) : Color.FromArgb(190, 40, 40);
            lblResult.BackColor = ok ? Color.FromArgb(235, 250, 240) : Color.FromArgb(253, 240, 240);
        }

        private void SetBusy(bool b)
        {
            busy = b;
            btnApply.Enabled = !b;
            btnRestore.Enabled = !b;
        }

        // ======================= 状态自动刷新 =======================
        private void RefreshTick(object sender, EventArgs e)
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (busy || refreshRunning) return;
            refreshRunning = true;
            var t = new Thread(delegate()
            {
                var snap = new Snapshot();
                try
                {
                    snap.Adb = FindAdb();
                    if (snap.Adb != null)
                    {
                        DeviceInfo dev = GetDevice(snap.Adb);
                        snap.DevState = dev.State;
                        if (dev.State == "device")
                        {
                            snap.SubId = GetSubId(snap.Adb);
                            snap.Country = GetCountry(snap.Adb);
                        }
                    }
                }
                catch { }
                try
                {
                    BeginInvoke((Action)delegate()
                    {
                        refreshRunning = false;
                        ApplySnapshot(snap);
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ApplySnapshot(Snapshot snap)
        {
            if (snap.Adb == null)
            {
                SetConn("未找到 adb", Color.FromArgb(190, 40, 40));
                SetNext("请把本程序放在 SimRegion 文件夹内运行（需包含 adb\\adb.exe）");
                return;
            }
            string st = snap.DevState == null ? "unknown" : snap.DevState;
            if (st == "unknown")
            {
                SetConn("状态检测中...", Color.FromArgb(64, 64, 64));
                SetNext("正在检测设备连接，请稍候");
            }
            else if (st == "device")
            {
                SetConn("设备已连接", Color.FromArgb(20, 130, 60));
                SetNext("点击下方「一键应用」开始，先选好地区");
                lblSubId.Text = snap.SubId >= 0 ? "SIM 信息：subId = " + snap.SubId : "SIM 信息：未能读取 subId";
                lblCurrent.Text = "当前 SIM 国家码：" + (snap.Country ?? "未知");
            }
            else if (st == "unauthorized")
            {
                SetConn("设备未授权", Color.FromArgb(200, 120, 20));
                SetNext("解锁手机，点击弹窗「允许 USB 调试」，建议勾选始终允许");
            }
            else if (st == "offline")
            {
                SetConn("设备离线", Color.FromArgb(200, 120, 20));
                SetNext("连接不稳定：换一个 USB 口或重新插拔数据线");
            }
            else
            {
                SetConn("设备未连接", Color.FromArgb(190, 40, 40));
                SetNext("请用数据线连接手机，并在开发者选项中开启 USB 调试");
            }
        }

        // ======================= 后台工作 =======================
        private void InitWorker()
        {
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_Completed;
        }

        private void StartApply()
        {
            if (busy || worker.IsBusy) return;
            SetBusy(true);
            AppendLog("========== 开始一键应用 ==========");
            worker.RunWorkerAsync("apply");
        }

        private void StartRestore()
        {
            if (busy || worker.IsBusy) return;
            SetBusy(true);
            AppendLog("========== 恢复原始配置 ==========");
            worker.RunWorkerAsync("restore");
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var w = (BackgroundWorker)sender;
            bool restore = (string)e.Argument == "restore";
            e.Result = restore ? DoRestore(w) : DoApply(w);
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            AppendLog((string)e.UserState);
        }

        private void worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            refreshRunning = false;
            RefreshStatus();
            if (e.Error != null)
            {
                AppendLog("异常：" + e.Error.Message);
                SetResult(false, "发生异常", e.Error.Message);
                return;
            }
            var r = (StepResult)e.Result;
            if (r.Ok)
            {
                lblCurrent.Text = "当前 SIM 国家码：" + r.Country;
                SetResult(true, r.Title, r.Advice);
            }
            else
            {
                SetResult(false, r.Title, r.Advice);
            }
        }

        // ---------- 一键应用 ----------
        private StepResult DoApply(BackgroundWorker w)
        {
            var res = new StepResult();
            try
            {
                string adb = FindAdb();
                if (adb == null)
                {
                    res.Ok = false; res.Title = "未找到 adb";
                    res.Advice = "请把本程序放在 SimRegion 文件夹内运行（需包含 adb\\adb.exe）。";
                    return res;
                }
                w.ReportProgress(0, "使用 adb：" + adb);

                DeviceInfo dev = GetDevice(adb);
                if (dev.State != "device")
                {
                    w.ReportProgress(0, "失败：设备状态 = " + dev.State);
                    res.Ok = false; res.Title = "手机未连接";
                    res.Advice = AdviceForDevice(dev.State);
                    return res;
                }
                w.ReportProgress(0, "设备已连接");

                string country = SelectedCountry();
                if (country.Length != 2 || !Regex.IsMatch(country, "^[A-Z]{2}$"))
                {
                    w.ReportProgress(0, "失败：国家码格式错误");
                    res.Ok = false; res.Title = "国家码格式错误";
                    res.Advice = "请选择预设地区，或自定义输入 2 位字母国家码（如 KR、TW）。";
                    return res;
                }

                // 第 1 步：检查 / 安装 APK
                w.ReportProgress(0, "—— 第 1 步：检查 APK 安装状态");
                CmdResult pm = RunAdb(adb, "shell pm path " + PKG, ADB_TIMEOUT_DUMPSYS);
                if (pm.Exit == 0 && pm.Out.Contains("package:"))
                {
                    w.ReportProgress(0, "APK 已安装：" + pm.Out.Trim());
                }
                else
                {
                    if (!File.Exists(ApkPath()))
                    {
                        w.ReportProgress(0, "失败：找不到 APK 文件 " + APK_NAME);
                        res.Ok = false; res.Title = "缺少 APK 文件";
                        res.Advice = "请把 " + APK_NAME + " 放在本程序同一文件夹内。";
                        return res;
                    }
                    w.ReportProgress(0, "未安装，开始安装 " + APK_NAME + " ...");
                    CmdResult ins = RunAdb(adb, "install -r \"" + ApkPath() + "\"", ADB_TIMEOUT_OPERATION);
                    w.ReportProgress(0, ins.Out);
                    if (!ins.Out.Contains("Success"))
                    {
                        res.Ok = false; res.Title = "APK 安装失败";
                        res.Advice = "请确认手机存储空间充足；也可以把 " + APK_NAME + " 发到手机手动安装。";
                        return res;
                    }
                    w.ReportProgress(0, "APK 安装成功");
                }

                // 第 2 步：读取 SIM
                w.ReportProgress(0, "—— 第 2 步：读取 SIM");
                int subId = GetSubId(adb);
                if (subId < 0)
                {
                    w.ReportProgress(0, "失败：未能读取 subId");
                    res.Ok = false; res.Title = "未识别到 SIM 卡";
                    res.Advice = "请确认 SIM 卡已插入且手机已解锁；双卡可在解锁后重试。";
                    return res;
                }
                w.ReportProgress(0, "subId = " + subId);

                // 第 3 步：应用覆盖
                w.ReportProgress(0, "—— 第 3 步：应用覆盖 " + country);
                CmdResult r = RunAdb(adb, "shell am instrument -w -r -e action apply -e subId " + subId + " -e country " + country.ToLowerInvariant() + " " + INSTR, ADB_TIMEOUT_OPERATION);
                w.ReportProgress(0, r.Out);

                if (r.Out.Contains("result=applied"))
                {
                    w.ReportProgress(0, "应用成功");
                    Thread.Sleep(800);
                    string cc = GetCountry(adb);
                    res.Ok = true; res.Title = "应用成功"; res.Country = cc;
                    res.Advice = "系统当前识别为 " + cc + "。如需还原请点「恢复原始配置」。";
                }
                else if (r.Out.Contains("result=error"))
                {
                    var em = Regex.Match(r.Out, @"error=([^\n]*)");
                    string err = em.Success ? em.Groups[1].Value.Trim() : "未知错误";
                    w.ReportProgress(0, "错误：" + err);
                    res.Ok = false; res.Title = "系统拒绝了覆盖操作";
                    res.Advice = AdviceForInstrumentError(err);
                }
                else
                {
                    res.Ok = false; res.Title = "命令已执行但未确认结果";
                    res.Advice = "请查看上方日志，或稍候观察「当前 SIM 国家码」。";
                }
            }
            catch (Exception ex)
            {
                w.ReportProgress(0, "异常：" + ex.Message);
                res.Ok = false; res.Title = "发生异常";
                res.Advice = ex.Message;
            }
            return res;
        }

        // ---------- 恢复原始配置 ----------
        private StepResult DoRestore(BackgroundWorker w)
        {
            var res = new StepResult();
            try
            {
                string adb = FindAdb();
                if (adb == null)
                {
                    res.Ok = false; res.Title = "未找到 adb";
                    res.Advice = "请把本程序放在 SimRegion 文件夹内运行（需包含 adb\\adb.exe）。";
                    return res;
                }
                DeviceInfo dev = GetDevice(adb);
                if (dev.State != "device")
                {
                    w.ReportProgress(0, "失败：设备状态 = " + dev.State);
                    res.Ok = false; res.Title = "手机未连接";
                    res.Advice = AdviceForDevice(dev.State);
                    return res;
                }
                w.ReportProgress(0, "设备已连接");

                int subId = GetSubId(adb);
                if (subId < 0)
                {
                    res.Ok = false; res.Title = "未识别到 SIM 卡";
                    res.Advice = "请确认 SIM 卡已插入且手机已解锁。";
                    return res;
                }
                w.ReportProgress(0, "subId = " + subId);

                w.ReportProgress(0, "—— 执行恢复命令");
                CmdResult r = RunAdb(adb, "shell am instrument -w -r -e action restore -e subId " + subId + " " + INSTR, ADB_TIMEOUT_OPERATION);
                w.ReportProgress(0, r.Out);

                if (r.Out.Contains("result=restored"))
                {
                    Thread.Sleep(800);
                    string cc = GetCountry(adb);
                    res.Ok = true; res.Title = "已恢复"; res.Country = cc;
                    res.Advice = "系统已回到运营商原始配置，当前识别为 " + cc + "。";
                }
                else if (r.Out.Contains("result=error"))
                {
                    var em = Regex.Match(r.Out, @"error=([^\n]*)");
                    res.Ok = false; res.Title = "恢复失败";
                    res.Advice = em.Success ? em.Groups[1].Value.Trim() : "请查看上方日志。";
                }
                else
                {
                    res.Ok = false; res.Title = "命令已执行但未确认";
                    res.Advice = "请查看上方日志。";
                }
            }
            catch (Exception ex)
            {
                w.ReportProgress(0, "异常：" + ex.Message);
                res.Ok = false; res.Title = "发生异常";
                res.Advice = ex.Message;
            }
            return res;
        }

        // ======================= 建议文案 =======================
        private static string AdviceForDevice(string state)
        {
            if (state == "unauthorized")
                return "手机弹窗未授权：解锁手机，点击「允许 USB 调试」，建议勾选始终允许。";
            if (state == "offline")
                return "连接不稳定：换一个 USB 口，或重新插拔数据线。";
            return "未检测到设备：① 换一根支持数据传输的数据线；② 电脑安装 Android USB 驱动；③ 确认开发者选项里的 USB 调试已开启。";
        }

        private static string AdviceForInstrumentError(string err)
        {
            if (err.Contains("SecurityException"))
                return "权限被系统拒绝：可尝试重启手机后重试；部分厂商 ROM 会限制 SIM 国家码覆盖。";
            if (err.Contains("IllegalArgument") || err.Contains("Invalid subscription"))
                return "subId 无效：请确认 SIM 卡正常，重插 SIM 或重启手机后重试。";
            return "此机型可能限制 SIM 国家码覆盖（部分厂商 ROM），命令已执行但未生效；可尝试重启手机后重试。";
        }

        // ======================= 自检模式（--check） =======================
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private static void SelfCheck()
        {
            AttachConsole(-1);
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                string adb = FindAdb();
                Console.WriteLine("ADB: " + (adb ?? "NOT FOUND"));
                if (adb == null) return;
                DeviceInfo dev = GetDevice(adb);
                Console.WriteLine("DEVICE: " + dev.State + " | " + dev.Line);
                Console.WriteLine("SUBID: " + GetSubId(adb));
                Console.WriteLine("COUNTRY: " + GetCountry(adb));
                Console.WriteLine("APK: " + (File.Exists(Path.Combine(AppContext.BaseDirectory, APK_NAME)) ? "OK" : "MISSING"));
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
        }

        // ======================= 入口 =======================
        [STAThread]
        private static void Main(string[] args)
        {
            EnsureEmbeddedResources();
            if (args.Length > 0 && args[0] == "--check")
            {
                SelfCheck();
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
