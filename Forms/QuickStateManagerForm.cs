namespace Derby
{
    public sealed class QuickStateManagerForm : Form
    {
        private const int SlotCount = 8;
        private readonly Func<int, QuickStateSlot?> getSlot;
        private readonly Func<int, QuickStateSlot> saveSlot;
        private readonly Func<int, bool> loadSlot;
        private readonly Func<int, bool> deleteSlot;
        private readonly Func<int, bool> confirmReplace;
        private readonly Func<int, bool> confirmDelete;
        private readonly SlotCard[] cards = new SlotCard[SlotCount];
        private readonly Label statusLabel = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolTip toolTip = new();

        internal QuickStateManagerForm(
            Func<int, QuickStateSlot?> getSlot,
            Func<int, QuickStateSlot> saveSlot,
            Func<int, bool> loadSlot,
            Func<int, bool> deleteSlot,
            Func<int, bool>? confirmReplace = null,
            Func<int, bool>? confirmDelete = null)
        {
            this.getSlot = getSlot ?? throw new ArgumentNullException(nameof(getSlot));
            this.saveSlot = saveSlot ?? throw new ArgumentNullException(nameof(saveSlot));
            this.loadSlot = loadSlot ?? throw new ArgumentNullException(nameof(loadSlot));
            this.deleteSlot = deleteSlot ?? throw new ArgumentNullException(nameof(deleteSlot));
            this.confirmReplace = confirmReplace ?? ConfirmReplace;
            this.confirmDelete = confirmDelete ?? ConfirmDelete;

            Text = "Quick State Manager - Paused";
            ClientSize = new Size(960, 720);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            EmulatorUiStyle.ApplyForm(this);
            KeyPreview = true;

            Controls.Add(CreateSlotWorkspace());
            Controls.Add(CreateFooter());
            Controls.Add(CreateHeader());
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
            };
        }

        private Control CreateHeader()
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = EmulatorUiStyle.Paper };
            Label title = EmulatorUiStyle.CreateTitle("QUICK STATE MANAGER");
            title.Location = new Point(22, 14);
            panel.Controls.Add(title);
            Label subtitle = EmulatorUiStyle.CreateSubtitle(
                "Eight persistent save slots. Saving again replaces the selected slot.");
            subtitle.Location = new Point(23, 43);
            panel.Controls.Add(subtitle);
            Label paused = EmulatorUiStyle.CreateSubtitle("PAUSED", accent: true);
            paused.AutoSize = false;
            paused.Dock = DockStyle.Right;
            paused.Width = 110;
            paused.Padding = new Padding(0, 18, 20, 0);
            paused.TextAlign = ContentAlignment.TopRight;
            panel.Controls.Add(paused);
            return panel;
        }

        private Control CreateSlotWorkspace()
        {
            var scroll = new Panel
            {
                Name = "QuickStateSlotScroll",
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(18, 4, 18, 8),
                BackColor = EmulatorUiStyle.Paper
            };
            var grid = new TableLayoutPanel
            {
                Name = "QuickStateSlotGrid",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int row = 0; row < 4; row++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            for (int index = 0; index < SlotCount; index++)
            {
                var card = new SlotCard(index, Save, LoadSelectedSlot, Delete, toolTip);
                cards[index] = card;
                card.Group.Margin = index % 2 == 0
                    ? new Padding(0, 0, 7, 10)
                    : new Padding(7, 0, 0, 10);
                grid.Controls.Add(card.Group, index % 2, index / 2);
                RefreshSlot(index);
            }

            scroll.Controls.Add(grid);
            return scroll;
        }

        private Control CreateFooter()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(18, 7, 18, 7),
                BackColor = EmulatorUiStyle.Paper
            };
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.ForeColor = EmulatorUiStyle.Muted;
            statusLabel.Text = "Choose an empty slot to save, or load an occupied slot.";
            var close = new Button { Text = "Close", Dock = DockStyle.Right, Width = 100 };
            EmulatorUiStyle.StyleButton(close, primary: true);
            close.Click += (_, _) => Close();
            panel.Controls.Add(statusLabel);
            panel.Controls.Add(close);
            return panel;
        }

        private void Save(int index)
        {
            if (getSlot(index) != null && !confirmReplace(index))
                return;

            saveSlot(index);
            RefreshSlot(index);
            statusLabel.ForeColor = EmulatorUiStyle.Accent;
            statusLabel.Text = $"Saved the current machine state to slot {index + 1}.";
        }

        private void LoadSelectedSlot(int index)
        {
            if (getSlot(index) == null || !loadSlot(index))
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Delete(int index)
        {
            if (getSlot(index) == null || !confirmDelete(index) || !deleteSlot(index))
                return;

            RefreshSlot(index);
            statusLabel.ForeColor = EmulatorUiStyle.Muted;
            statusLabel.Text = $"Deleted Quick State slot {index + 1}.";
        }

        private void RefreshSlot(int index) => cards[index].Update(getSlot(index));

        private bool ConfirmReplace(int index) =>
            MessageBox.Show(
                this,
                $"Replace the Quick State currently stored in slot {index + 1}?",
                "Replace Quick State",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        private bool ConfirmDelete(int index) =>
            MessageBox.Show(
                this,
                $"Permanently delete Quick State slot {index + 1}?",
                "Delete Quick State",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        private sealed class SlotCard
        {
            private readonly int index;
            private readonly Label emptyLabel = new();
            private readonly PictureBox thumbnail = new();
            private readonly Label media = new();
            private readonly Label machine = new();
            private readonly Label captured = new();
            private readonly Button save = new();
            private readonly Button load = new();
            private readonly Button delete = new();

            public SlotCard(
                int index,
                Action<int> saveSlot,
                Action<int> loadSlot,
                Action<int> deleteSlot,
                ToolTip toolTip)
            {
                this.index = index;
                Group = new GroupBox
                {
                    Name = $"QuickStateSlot{index + 1}",
                    Text = $"Slot {index + 1}",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(10, 7, 10, 9)
                };
                EmulatorUiStyle.StyleGroup(Group);

                delete.Name = $"QuickStateDelete{index + 1}";
                delete.Text = "X";
                delete.Size = new Size(22, 22);
                delete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                delete.TabStop = true;
                delete.AccessibleName = $"Delete Quick State slot {index + 1}";
                delete.Font = EmulatorUiStyle.UiFont(8F, FontStyle.Bold);
                EmulatorUiStyle.StyleButton(delete);
                delete.Click += (_, _) => deleteSlot(index);
                toolTip.SetToolTip(delete, $"Delete Quick State slot {index + 1}");
                Group.SizeChanged += (_, _) => PositionDeleteButton();

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                var preview = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 2, 10, 1)
                };
                thumbnail.Name = $"QuickStateThumbnail{index + 1}";
                thumbnail.Dock = DockStyle.Fill;
                thumbnail.SizeMode = PictureBoxSizeMode.Zoom;
                thumbnail.BackColor = Color.Black;
                emptyLabel.Dock = DockStyle.Fill;
                emptyLabel.Text = "EMPTY";
                emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
                emptyLabel.ForeColor = Color.FromArgb(175, 185, 181);
                emptyLabel.Font = EmulatorUiStyle.MonoFont(9F, FontStyle.Bold);
                emptyLabel.BackColor = Color.Black;
                preview.Controls.Add(thumbnail);
                preview.Controls.Add(emptyLabel);

                var details = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                details.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                details.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                details.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                ConfigureLabel(media, 9F, FontStyle.Bold, EmulatorUiStyle.Ink);
                ConfigureLabel(machine, 8.5F, FontStyle.Regular, EmulatorUiStyle.Muted);
                ConfigureLabel(captured, 8.5F, FontStyle.Regular, EmulatorUiStyle.Muted);
                media.Name = $"QuickStateMedia{index + 1}";
                machine.Name = $"QuickStateMachine{index + 1}";
                captured.Name = $"QuickStateCaptured{index + 1}";
                details.Controls.Add(media, 0, 0);
                details.Controls.Add(machine, 0, 1);
                details.Controls.Add(captured, 0, 2);

                var actions = new TableLayoutPanel
                {
                    Dock = DockStyle.Right,
                    Width = 178,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = Padding.Empty,
                    Padding = new Padding(0, 0, 0, 4)
                };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                save.Name = $"QuickStateSave{index + 1}";
                save.Dock = DockStyle.Fill;
                save.Margin = new Padding(0, 1, 3, 1);
                load.Name = $"QuickStateLoad{index + 1}";
                load.Text = "Load";
                load.Dock = DockStyle.Fill;
                load.Margin = new Padding(3, 1, 0, 1);
                EmulatorUiStyle.StyleButton(save);
                EmulatorUiStyle.StyleButton(load, primary: true);
                save.Click += (_, _) => saveSlot(index);
                load.Click += (_, _) => loadSlot(index);
                actions.Controls.Add(save, 0, 0);
                actions.Controls.Add(load, 1, 0);
                details.Controls.Add(actions, 0, 3);

                layout.Controls.Add(preview, 0, 0);
                layout.Controls.Add(details, 1, 0);
                Group.Controls.Add(layout);
                Group.Controls.Add(delete);
                PositionDeleteButton();
                delete.BringToFront();
            }

            public GroupBox Group { get; }

            public void Update(QuickStateSlot? slot)
            {
                bool occupied = slot != null;
                thumbnail.Image = slot?.Thumbnail;
                emptyLabel.Visible = !occupied;
                if (!occupied)
                    emptyLabel.BringToFront();
                media.Text = occupied ? slot!.MediaDisplayName ?? "No media loaded" : "Empty slot";
                machine.Text = occupied ? CleanProfileName(slot!.RomSet.Name) : "No machine state captured";
                captured.Text = occupied
                    ? slot!.CapturedAtUtc.ToLocalTime().ToString("g")
                    : "Available for a new Quick State";
                save.Text = occupied ? "Replace" : "Save";
                EmulatorUiStyle.StyleButton(save, primary: !occupied);
                EmulatorUiStyle.StyleButton(load, primary: occupied);
                load.Enabled = occupied;
                delete.Visible = occupied;
                if (occupied)
                    delete.BringToFront();
            }

            private void PositionDeleteButton() =>
                delete.Location = new Point(Math.Max(0, Group.ClientSize.Width - delete.Width - 6), 0);

            private static void ConfigureLabel(Label label, float size, FontStyle style, Color color)
            {
                label.Dock = DockStyle.Fill;
                label.AutoEllipsis = true;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Font = EmulatorUiStyle.UiFont(size, style);
                label.ForeColor = color;
            }

            private static string CleanProfileName(string name) =>
                name.EndsWith(" (bundled)", StringComparison.Ordinal)
                    ? name[..^10]
                    : name;
        }
    }
}
