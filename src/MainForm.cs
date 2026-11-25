using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RightSlim
{
    public sealed class MainForm : Form
    {
        private readonly ToolStripComboBox _ctxFilter = new ToolStripComboBox();
        private readonly ToolStripTextBox _search = new ToolStripTextBox();
        private readonly ListView _list = new ListView();
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _statusLabel = new ToolStripStatusLabel();
        private readonly Label _summary = new Label();
        private readonly Label _detail = new Label();

        private List<MenuItem> _all = new List<MenuItem>();
        private bool _busy;
        private bool _suppressCheck;

        private IEnumerable<MenuItem> Pending
        {
            get { return _all.Where(x => x.DesiredEnabled != x.Enabled); }
        }
        private int PendingCount { get { return _all.Count(x => x.DesiredEnabled != x.Enabled); } }

        public MainForm()
        {
            Text = "右键菜单瘦身大师";
            Width = 1040;
            Height = 640;
            MinimumSize = new Size(760, 460);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildLayout();

            Load += (s, e) => Reload();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.White,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));   // 标题
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // 工具栏
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 列表
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));   // 说明 + 统计 + 状态

            BuildHeader(root);
            BuildToolbar(root);
            BuildList(root);
            BuildStatus(root);

            Controls.Add(root);
        }

        // ---------- 布局 ----------

        private void BuildHeader(TableLayoutPanel root)
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(16, 10, 16, 8) };

            var title = new Label
            {
                Text = "右键菜单瘦身",
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Location = new Point(14, 8),
            };
            var sub = new Label
            {
                Text = "打勾 = 在右键菜单中显示，取消勾选 = 隐藏。无需接触注册表。",
                ForeColor = Color.FromArgb(110, 117, 125),
                AutoSize = true,
                Location = new Point(16, 36),
            };
            header.Controls.Add(title);
            header.Controls.Add(sub);
            root.Controls.Add(header, 0, 0);
        }

        private void BuildToolbar(TableLayoutPanel root)
        {
            var strip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(10, 4, 10, 4),
                ImageScalingSize = new Size(16, 16),
                RenderMode = ToolStripRenderMode.System,
            };

            _ctxFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _ctxFilter.Width = 170;
            _ctxFilter.Items.Add("全部位置");
            _ctxFilter.SelectedIndex = 0;
            _ctxFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            _search.Width = 190;
            _search.ToolTipText = "按名称或来源搜索";
            _search.TextChanged += (s, e) => ApplyFilter();

            var btnRefresh = new ToolStripButton("刷新") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnRefresh.Click += (s, e) => Reload();

            var btnAllOn = new ToolStripButton("全部显示") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnAllOn.Click += (s, e) => BulkSet(true);

            var btnAllOff = new ToolStripButton("全部隐藏") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnAllOff.Click += (s, e) => BulkSet(false);

            var btnPreview = new ToolStripButton("预览效果") { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
            btnPreview.Click += (s, e) => ShowPreview();

            var btnApply = new ToolStripButton("保存并应用") { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
            btnApply.Click += (s, e) => ApplyAll();

            strip.Items.Add(new ToolStripLabel("位置："));
            strip.Items.Add(_ctxFilter);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(new ToolStripLabel("搜索："));
            strip.Items.Add(_search);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(btnRefresh);
            strip.Items.Add(btnAllOn);
            strip.Items.Add(btnAllOff);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(btnPreview);
            strip.Items.Add(btnApply);

            root.Controls.Add(strip, 0, 1);
        }

        private void BuildList(TableLayoutPanel root)
        {
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.CheckBoxes = true;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.ShowItemToolTips = true;
            _list.BorderStyle = BorderStyle.None;
            _list.Font = new Font("Microsoft YaHei UI", 9f);
            _list.Columns.Add("显示", 52, HorizontalAlignment.Center);
            _list.Columns.Add("名称", 220, HorizontalAlignment.Left);
            _list.Columns.Add("作用说明", 260, HorizontalAlignment.Left);
            _list.Columns.Add("出现位置", 130, HorizontalAlignment.Left);
            _list.Columns.Add("类型", 56, HorizontalAlignment.Center);
            _list.Columns.Add("来源", 260, HorizontalAlignment.Left);
            _list.ItemCheck += OnItemCheck;
            _list.SelectedIndexChanged += OnSelectionChanged;

            root.Controls.Add(_list, 0, 2);
        }

        private void BuildStatus(TableLayoutPanel root)
        {
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
            };
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // 选中项说明
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // 统计
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 状态栏

            _detail.Dock = DockStyle.Fill;
            _detail.TextAlign = ContentAlignment.MiddleLeft;
            _detail.Padding = new Padding(16, 0, 16, 0);
            _detail.Font = new Font("Microsoft YaHei UI", 9f);
            _detail.ForeColor = Color.FromArgb(33, 37, 41);
            _detail.BackColor = Color.FromArgb(255, 250, 235);
            _detail.Text = "点击任意一项，这里会说明它是做什么的。";
            _detail.AutoEllipsis = true;
            _detail.Visible = true;

            _summary.Dock = DockStyle.Fill;
            _summary.Height = 26;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.Padding = new Padding(16, 0, 0, 0);
            _summary.ForeColor = Color.FromArgb(73, 80, 87);
            _summary.BackColor = Color.FromArgb(248, 249, 251);

            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _status.SizingGrip = false;
            _status.Items.Add(_statusLabel);

            bottom.Controls.Add(_detail, 0, 0);
            bottom.Controls.Add(_summary, 0, 1);
            bottom.Controls.Add(_status, 0, 2);
            root.Controls.Add(bottom, 0, 3);
        }

        // ---------- 数据 ----------

        private void Reload()
        {
            var selectedCtx = _ctxFilter.SelectedItem as string;
            SetBusy(true, "正在读取右键菜单…");
            try
            {
                _all = RegistryHelper.Scan();
                foreach (var mi in _all) mi.DesiredEnabled = mi.Enabled;

                var ctxs = _all.Select(x => x.ContextName).Distinct().ToList();
                ctxs.Sort(StringComparer.OrdinalIgnoreCase);

                _ctxFilter.Items.Clear();
                _ctxFilter.Items.Add("全部位置");
                foreach (var c in ctxs) _ctxFilter.Items.Add(c);
                if (selectedCtx != null && _ctxFilter.Items.Contains(selectedCtx))
                    _ctxFilter.SelectedItem = selectedCtx;
                else
                    _ctxFilter.SelectedIndex = 0;

                ApplyFilter();
                SetStatus(string.Format("共 {0} 项，已隐藏 {1} 项。", _all.Count, _all.Count(x => !x.Enabled)));
            }
            catch (Exception ex)
            {
                SetStatus("读取失败：" + ex.Message);
                MessageBox.Show(this, "读取右键菜单失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void ApplyFilter()
        {
            string ctx = _ctxFilter.SelectedItem as string;
            string q = (_search.Text ?? "").Trim();

            IEnumerable<MenuItem> data = _all;
            if (!string.IsNullOrEmpty(ctx) && ctx != "全部位置")
                data = data.Where(x => x.ContextName == ctx);
            if (q.Length > 0)
            {
                data = data.Where(x =>
                    (x.DisplayName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.Detail ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _list.BeginUpdate();
            _suppressCheck = true;
            try
            {
                _list.Items.Clear();
                foreach (var mi in data)
                {
                    var lvi = new ListViewItem("") { Tag = mi, Checked = mi.DesiredEnabled, ToolTipText = mi.FullRegistryPath };
                    lvi.SubItems.Add(mi.DisplayName);
                    lvi.SubItems.Add(Explain.Describe(mi));
                    lvi.SubItems.Add(mi.ContextName);
                    lvi.SubItems.Add(mi.KindText);
                    lvi.SubItems.Add(mi.Detail ?? "");
                    ApplyRowStyle(lvi, mi);
                    _list.Items.Add(lvi);
                }
            }
            finally
            {
                _list.EndUpdate();
                _suppressCheck = false;
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int shown = _list.Items.Count;
            int off = _list.Items.Cast<ListViewItem>().Count(i => !i.Checked);
            int pending = PendingCount;
            _summary.Text = string.Format(
                "当前列表 {0} 项，其中 {1} 项不显示。预览里的修改共 {2} 项尚未保存，点「保存并应用」才真正生效。",
                shown, off, pending);
            _summary.ForeColor = pending > 0 ? Color.FromArgb(176, 96, 0) : Color.FromArgb(73, 80, 87);
        }

        // ---------- 交互 ----------

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_busy || _suppressCheck) return;
            var lvi = _list.Items[e.Index];
            var mi = lvi.Tag as MenuItem;
            if (mi == null) return;

            bool target = e.NewValue == CheckState.Checked;
            if (target == mi.DesiredEnabled) return; // 无变化

            // 只记录意图，不碰注册表；真正写盘在「保存并应用」
            mi.DesiredEnabled = target;
            BeginInvoke(new Action(() =>
            {
                ApplyRowStyle(lvi, mi);
                UpdateSummary();
                ShowDetail(mi);
            }));
        }

        private void ApplyRowStyle(ListViewItem lvi, MenuItem mi)
        {
            lvi.ToolTipText = mi.FullRegistryPath;
            if (Explain.IsCore(mi))
                lvi.ToolTipText += "\n⚠ 这是系统常用的核心操作，隐藏后会影响日常使用。";

            bool pending = mi.DesiredEnabled != mi.Enabled;
            if (pending)
            {
                lvi.BackColor = mi.DesiredEnabled ? Color.FromArgb(235, 247, 235) : Color.FromArgb(255, 241, 236);
                lvi.ForeColor = Color.FromArgb(33, 37, 41);
            }
            else
            {
                lvi.BackColor = Color.White;
                lvi.ForeColor = Color.FromArgb(73, 80, 87);
            }
        }

        private void ShowDetail(MenuItem mi)
        {
            string state;
            if (mi.DesiredEnabled == mi.Enabled) state = mi.Enabled ? "当前：显示" : "当前：已隐藏";
            else state = mi.DesiredEnabled ? "待应用：改为显示" : "待应用：改为隐藏";

            var parts = new List<string>();
            parts.Add("【" + mi.DisplayName + "】");
            parts.Add("作用： " + Explain.Describe(mi));
            parts.Add("位置： " + mi.ContextName + "　类型： " + mi.KindText);
            if (!string.IsNullOrEmpty(mi.Detail)) parts.Add("来源： " + mi.Detail);
            parts.Add(state);
            if (!mi.DesiredEnabled && Explain.IsCore(mi))
                parts.Add("⚠ 警告：这是系统常用的核心操作，隐藏后日常右键会不方便。");

            _detail.Text = string.Join("　|　", parts);
            _detail.ForeColor = (!mi.DesiredEnabled && Explain.IsCore(mi))
                ? Color.FromArgb(176, 0, 0) : Color.FromArgb(33, 37, 41);
            _detail.Show();
        }

        private void BulkSet(bool on)
        {
            var targets = _list.Items.Cast<ListViewItem>()
                .Select(i => i.Tag as MenuItem)
                .Where(m => m != null && m.DesiredEnabled != on)
                .ToList();
            if (targets.Count == 0) { SetStatus("当前列表已是该状态。"); return; }

            foreach (var mi in targets) mi.DesiredEnabled = on;
            ApplyFilter();
            SetStatus(string.Format("{0} 项已标记为「{1}」，尚未保存。点「预览效果」查看，或「保存并应用」生效。",
                targets.Count, on ? "显示" : "隐藏"));
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0) return;
            var mi = _list.SelectedItems[0].Tag as MenuItem;
            if (mi != null) ShowDetail(mi);
        }

        // ---------- 预览与保存 ----------

        private void ShowPreview()
        {
            var pending = Pending.ToList();
            bool applyRequested = false;
            using (var dlg = new PreviewForm(_all, pending, _ctxFilter.SelectedItem as string))
            {
                dlg.ApplyRequested = () => { applyRequested = true; };
                dlg.ShowDialog(this);
            }
            if (applyRequested) ApplyAll();
        }

        private void ApplyAll()
        {
            var pending = Pending.ToList();
            if (pending.Count == 0)
            {
                MessageBox.Show(this, "没有需要保存的修改。\n先勾选/取消勾选，再点「保存并应用」。",
                    "无修改", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var hideCore = pending.Where(x => !x.DesiredEnabled && Explain.IsCore(x)).ToList();
            string tip = string.Format("即将应用 {0} 处修改：\n\n  显示 {1} 项\n  隐藏 {2} 项\n\n应用后立即生效，可随时改回来。",
                pending.Count,
                pending.Count(x => x.DesiredEnabled),
                pending.Count(x => !x.DesiredEnabled));
            if (hideCore.Count > 0)
                tip += "\n\n⚠ 其中 " + hideCore.Count + " 项是系统常用操作，隐藏后日常右键会不方便：\n" +
                       string.Join("、", hideCore.Take(5).Select(x => x.DisplayName)) +
                       (hideCore.Count > 5 ? " 等" : "");

            if (MessageBox.Show(this, tip, "确认保存并应用", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning) != DialogResult.OK) return;

            int ok = 0;
            var errors = new List<string>();
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (var mi in pending)
                {
                    try { RegistryHelper.SetEnabled(mi, mi.DesiredEnabled); mi.Enabled = mi.DesiredEnabled; ok++; }
                    catch (Exception ex) { errors.Add(mi.DisplayName + "：" + ex.Message); }
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            ApplyFilter();
            SetStatus(string.Format("已应用 {0} 项修改{1}。", ok, errors.Count > 0 ? "，" + errors.Count + " 项失败" : ""));
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors.Take(10)), "部分项应用失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetBusy(bool busy, string msg)
        {
            _busy = busy;
            if (busy)
            {
                _list.Enabled = false;
                if (msg != null) SetStatus(msg);
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();
            }
            else
            {
                _list.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void SetStatus(string msg)
        {
            _statusLabel.Text = " " + msg;
        }
    }
}