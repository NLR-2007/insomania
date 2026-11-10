using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Insominia
{
    /// <summary>
    /// Lets the user tick the apps that must keep running at full speed while
    /// the lid is shut. Everything else gets slowed down to save battery.
    /// </summary>
    internal sealed class AppPickerForm : Form
    {
        private readonly CheckedListBox _list = new CheckedListBox();
        private readonly Label _hint = new Label();
        private List<AppEntry> _apps = new List<AppEntry>();

        /// <summary>Process names the user wants kept at full speed.</summary>
        public HashSet<string> Selected { get; private set; }

        public AppPickerForm(HashSet<string> alreadySelected)
        {
            Selected = new HashSet<string>(alreadySelected, StringComparer.OrdinalIgnoreCase);

            Text = "Insominia - which apps must keep running?";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(560, 480);
            MinimumSize = new Size(460, 380);
            Icon = TrayIcons.Get(TrayState.Active);

            Label title = new Label
            {
                Text = "Tick the apps that must keep working while the lid is closed.",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(12, 8, 12, 0),
                Font = new Font(Font, FontStyle.Bold),
            };

            Label sub = new Label
            {
                Text = "Everything you do not tick will be slowed down to save battery. " +
                       "Nothing is closed and no work is lost - slowed apps speed straight back up " +
                       "when you switch Insominia off.",
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(12, 2, 12, 6),
                ForeColor = SystemColors.GrayText,
            };

            _list.Dock = DockStyle.Fill;
            _list.CheckOnClick = true;
            _list.IntegralHeight = false;
            _list.Margin = new Padding(12);
            _list.ItemCheck += delegate { BeginInvoke((MethodInvoker)UpdateHint); };

            _hint.Dock = DockStyle.Bottom;
            _hint.Height = 24;
            _hint.Padding = new Padding(12, 4, 12, 0);
            _hint.ForeColor = SystemColors.GrayText;

            Panel listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
            listHost.Controls.Add(_list);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(12, 8, 12, 8),
            };

            Button ok = new Button { Text = "Save", Width = 90, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
            Button none = new Button { Text = "Untick all", Width = 90 };
            Button refresh = new Button { Text = "Refresh", Width = 90 };

            ok.Click += delegate { Harvest(); };
            none.Click += delegate
            {
                for (int i = 0; i < _list.Items.Count; i++) _list.SetItemChecked(i, false);
                UpdateHint();
            };
            refresh.Click += delegate { Harvest(); Reload(); };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(none);
            buttons.Controls.Add(refresh);

            Controls.Add(listHost);
            Controls.Add(_hint);
            Controls.Add(buttons);
            Controls.Add(sub);
            Controls.Add(title);

            AcceptButton = ok;
            CancelButton = cancel;

            Reload();
        }

        private void Reload()
        {
            _apps = ProcessThrottler.ListVisibleApps();
            _list.BeginUpdate();
            _list.Items.Clear();

            foreach (AppEntry app in _apps)
                _list.Items.Add(app, Selected.Contains(app.ProcessName));

            _list.EndUpdate();
            UpdateHint();
        }

        private void Harvest()
        {
            Selected.Clear();
            foreach (object item in _list.CheckedItems)
            {
                AppEntry app = item as AppEntry;
                if (app != null) Selected.Add(app.ProcessName);
            }
        }

        private void UpdateHint()
        {
            int kept = _list.CheckedItems.Count;
            int slowed = _list.Items.Count - kept;
            _hint.Text = kept + " app(s) will keep full speed, " + slowed + " will be slowed down.";
        }
    }
}
