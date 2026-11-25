using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RightSlim
{
    /// <summary>预览：左「当前右键菜单」，右「保存后的右键菜单」，逐项对比增删。</summary>
    public sealed class PreviewForm : Form
    {
        private readonly List<MenuItem> _all;
        private readonly List<MenuItem> _pending;

        /// <summary>用户在预览里点“保存并应用”时触发，由主界面执行落盘。</summary>
        public Action ApplyRequested;

        public PreviewForm(List<MenuItem> all, List<MenuItem> pending, string focusContext)
        {
            _all = all;
            _pending = pending;

            Text = "预览效果 — 保存前后的右键菜单对比";
            Width = 980;
            Height = 660;
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Build(focusContext);
        }

        private void Build(string focusContext)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            // 顶部说明
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(16, 10, 16, 8) };
            head.Controls.Add(new Label
            {
                Text = "预览：左边是现在的右键菜单，右边是保存后的样子",
                Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 8),
            });
            var pendingDesc = _pending.Count == 0
                ? "还没有任何改动。返回勾选后再来看。"
                : string.Format("共有 {0} 处改动：新增显示 {1} 项（绿色），被隐藏 {2} 项（红色）。确认无误后回到主界面点「保存并应用」。",
                    _pending.Count,
                    _pending.Count(x => x.DesiredEnabled),
                    _pending.Count(x => !x.DesiredEnabled));
            head.Controls.Add(new Label
            {
                Text = pendingDesc,
                ForeColor = Color.FromArgb(110, 117, 125),
                AutoSize = true,
                Location = new Point(16, 36),
            });
            root.Controls.Add(head, 0, 0);

            // 左右对比
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            string ctx = string.IsNullOrEmpty(focusContext) || focusContext == "全部位置" ? null : focusContext;

            var before = new MenuPreviewPanel("现在（未保存）") { Dock = DockStyle.Fill, Margin = new Padding(4) };
            before.Fill(BuildBefore(ctx));

            var after = new MenuPreviewPanel("保存后") { Dock = DockStyle.Fill, Margin = new Padding(4) };
            after.Fill(BuildAfter(ctx));

            split.Controls.Add(before, 0, 0);
            split.Controls.Add(after, 1, 0);
            root.Controls.Add(split, 0, 1);

            // 底部按钮
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 12, 10),
            };
            var btnClose = new Button { Text = "关闭", Width = 96, Height = 30, DialogResult = DialogResult.Cancel };
            bar.Controls.Add(btnClose);

            if (_pending.Count > 0)
            {
                var btnApply = new Button { Text = "保存并应用", Width = 120, Height = 30 };
                btnApply.Click += (s, e) =>
                {
                    if (ApplyRequested != null) ApplyRequested();
                    DialogResult = DialogResult.OK;
                    Close();
                };
                bar.Controls.Add(btnApply);
            }
            root.Controls.Add(bar, 0, 2);

            Controls.Add(root);
            AcceptButton = null;
            CancelButton = btnClose;
        }

        private List<PreviewRow> BuildBefore(string ctx)
        {
            return Filtered(ctx).Select(mi => new PreviewRow
            {
                Name = mi.DisplayName,
                Desc = Explain.Describe(mi),
                Shown = mi.Enabled,
                Change = RowChange.None,
            }).ToList();
        }

        private List<PreviewRow> BuildAfter(string ctx)
        {
            return Filtered(ctx).Select(mi => new PreviewRow
            {
                Name = mi.DisplayName,
                Desc = Explain.Describe(mi),
                Shown = mi.DesiredEnabled,
                Change = !mi.DesiredEnabled && mi.Enabled ? RowChange.Hidden
                       : mi.DesiredEnabled && !mi.Enabled ? RowChange.Added
                       : RowChange.None,
            }).ToList();
        }

        private IEnumerable<MenuItem> Filtered(string ctx)
        {
            IEnumerable<MenuItem> data = _all;
            if (ctx != null) data = data.Where(x => x.ContextName == ctx);
            return data;
        }

        // ---------- 行模型 ----------

        internal enum RowChange { None, Added, Hidden }

        internal sealed class PreviewRow
        {
            public string Name;
            public string Desc;
            public bool Shown;
            public RowChange Change;
        }

        // ---------- 单侧菜单面板 ----------

        internal sealed class MenuPreviewPanel : Panel
        {
            private readonly string _title;
            private readonly ListView _lv = new ListView();

            public MenuPreviewPanel(string title)
            {
                _title = title;
                BackColor = Color.White;
                BorderStyle = BorderStyle.FixedSingle;

                var top = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(240, 242, 245),
                };

                _lv.Dock = DockStyle.Fill;
                _lv.View = View.Details;
                _lv.HeaderStyle = ColumnHeaderStyle.None;
                _lv.FullRowSelect = true;
                _lv.HideSelection = true;
                _lv.MultiSelect = false;
                _lv.BorderStyle = BorderStyle.None;
                _lv.Font = new Font("Microsoft YaHei UI", 9f);
                _lv.Columns.Add("menu", 400, HorizontalAlignment.Left);

                Controls.Add(_lv);
                Controls.Add(top);
                _titleLabel = top;
            }

            private readonly Label _titleLabel;

            public void Fill(List<PreviewRow> rows)
            {
                int shown = rows.Count(r => r.Shown);
                int hidden = rows.Count(r => !r.Shown);
                _titleLabel.Text = string.Format("{0}　—— 显示 {1} 项，隐藏 {2} 项", _title, shown, hidden);

                _lv.BeginUpdate();
                try
                {
                    _lv.Items.Clear();
                    foreach (var r in rows)
                    {
                        var lvi = new ListViewItem(r.Shown ? r.Name : r.Name + "　（不显示）");
                        lvi.ToolTipText = r.Desc;

                        if (!r.Shown)
                        {
                            // 隐藏项：灰化、删除线
                            lvi.ForeColor = Color.FromArgb(160, 160, 160);
                            lvi.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Strikeout);
                        }

                        switch (r.Change)
                        {
                            case RowChange.Added:
                                lvi.BackColor = Color.FromArgb(226, 246, 226);
                                lvi.ForeColor = Color.FromArgb(20, 110, 20);
                                lvi.Text = "＋ " + r.Name + "　（将出现）";
                                lvi.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
                                break;
                            case RowChange.Hidden:
                                lvi.BackColor = Color.FromArgb(255, 232, 226);
                                lvi.ForeColor = Color.FromArgb(160, 30, 30);
                                lvi.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Strikeout);
                                break;
                        }

                        _lv.Items.Add(lvi);
                    }
                }
                finally
                {
                    _lv.EndUpdate();
                }
            }
        }
    }
}