namespace Derby
{
    public sealed class EmulatorHelpForm : Form
    {
        private readonly ListBox sectionList = new();
        private readonly GroupBox detailsGroup = new();
        private readonly Panel detailsPanel = new();

        private static readonly HelpSection[] Sections =
        [
            new("Main shortcuts",
            [
                ("F1", "Show or close this help window"),
                ("F2", "Show or hide compact FPS, model, tape, and display diagnostics"),
                ("F3", "Reset and cycle 128K, 48K, +2 Grey, +2A, and +3 machine profiles"),
                ("F4", "Cycle 1x Native, 2x Enhanced, and 3x Enhanced"),
                ("F5", "Play, stop, or resume the tape"),
                ("Shift+F5", "Open Tape Transport, even with an empty deck"),
                ("F6", "Reset and load a .tap or .tzx tape as a new game"),
                ("F7", "Insert or change a tape without resetting; the tape stays stopped"),
                ("F8", "Open Disk Drive for read-only +3 disk images"),
                ("F9", "Load a .sna, .z80, or .szx snapshot, or play an .rzx recording"),
                ("F10", "Open the paused eight-slot Quick State Manager (not available on +3)"),
                ("F11", "Open the paused debugger and disassembler"),
                ("F12", "Save a diagnostic dump and briefly show an on-screen badge"),
                ("Shift+F12", "Open Debug Dumps to view saved files or locate them in Explorer"),
                ("Right-click", "Open the emulator menu, including Keyboard, Joystick, Mouse, and Poke Manager")
            ]),
            new("Tape and disk",
            [
                ("Load Tape", "Use F6 to start a new game from the selected tape"),
                ("Insert / Change", "Use F7 to keep the running game. Press F5 or Play tape when ready."),
                ("Tape Transport", "Use Shift+F5 to rewind, jump to a named section, or save a session bookmark"),
                ("Tape at End", "The tape is still in the deck. Rewind or choose a position before playing."),
                ("Choose tape", "Starts a new game with an empty deck; otherwise offers Change tape or Start new game"),
                ("Disk Drive", "Use F8 to insert, replace, or eject a read-only .dsk image in drive A:"),
                ("Disk loading", "After inserting a disk, close the drive window and choose Loader in the +3 ROM menu")
            ]),
            new("Debugger window",
            [
                ("Arrow keys", "Move through listing rows; Enter follows a direct branch target"),
                ("Alt+Left / Right", "Move backward or forward through address history"),
                ("Ctrl+G / Ctrl+P", "Focus address entry or return to the captured PC"),
                ("Ctrl+F / F3", "Focus search or find the next address, byte, or mnemonic match"),
                ("F5", "Run or request a pause at the next instruction boundary"),
                ("F9", "Toggle a breakpoint on the selected instruction"),
                ("F10 / F11", "Step over a call or step into one instruction"),
                ("Shift+F11", "Step out using the return address currently on the stack"),
                ("Ctrl+F10", "Run to the selected instruction without adding a breakpoint"),
                ("Ctrl+R", "Refresh the immutable capture while remaining paused"),
                ("Ctrl+C", "Copy selected rows with capture metadata"),
                ("Ctrl+Shift+C", "Copy every displayed row with capture metadata"),
                ("Memory view", "Select mapped 64K, ROM banks, or RAM banks for the listing"),
                ("CPU state links", "Inspect register addresses in Memory; use PC or stack values to navigate the listing"),
                ("Memory tab", "Inspect hex/ASCII bytes from registers, stack, listing, or targets"),
                ("Ctrl+L", "Edit the label at the selected address"),
                ("Right-click", "Find label, code/data, memory inspection, and listing commands"),
                ("Ctrl+E", "Export a complete 64K mapped or 16K physical-bank listing"),
                ("Esc", "Close the debugger")
            ]),
            new("Spectrum keyboard",
            [
                ("Letters / numbers", "Map to their Spectrum keys"),
                ("Enter / Space", "Map directly to the Spectrum keyboard"),
                ("Shift", "Maps to Spectrum CAPS SHIFT"),
                ("Ctrl / Alt", "Map to Spectrum SYMBOL SHIFT"),
                ("Keyboard", "Open the on-screen keyboard from the right-click menu"),
                ("Page Up / Down", "Select the 48K or 128K layout when the on-screen keyboard has focus"),
                ("PC punctuation", "Maps common BASIC punctuation to Spectrum SYMBOL SHIFT chords"),
                ("Arrow keys", "Provide cursor-style Spectrum input chords")
            ]),
            new("Joystick",
            [
                ("Controller 1 / 2", "Configure and monitor two independent host controllers"),
                ("Off", "Disconnect that controller channel from the emulated machine"),
                ("Automatic", "Keep the current device, then prefer its profile or use an unclaimed device"),
                ("Kempston", "Expose active-high directions and Fire through port 1FH"),
                ("Cursor", "Map directions and Fire to keys 5, 8, 6, 7, and 0"),
                ("Sinclair", "Sinclair 1 uses keys 1-5 (socket 2); Sinclair 2 uses keys 6-0 (socket 1)"),
                ("Configure", "Map both controllers; duplicate devices and interfaces are rejected"),
                ("Identify", "Move a control or press a button to confirm which device you are holding")
            ]),
            new("Mouse",
            [
                ("Mouse submenu", "Choose Off, AMX Mouse, or Kempston Mouse to match the running software"),
                ("Capture", "Click inside the display to capture the Windows pointer"),
                ("Esc", "Release the pointer"),
                ("AMX Mouse", "Shares the Kempston joystick port; choose Off or Kempston Mouse to free it")
            ]),
            new("Poke Manager",
            [
                ("Poke Manager", "Open from the right-click menu; emulation pauses while it is open"),
                ("Library", "Choose the correct game and release, then preview the checked pokes"),
                ("One-off", "Enter a mapped address or physical RAM bank and offset, then preview the write"),
                ("Restore Last", "Undo the most recent application if the changed bytes still match")
            ])
        ];

        public EmulatorHelpForm()
        {
            Text = "DERBY Help";
            ClientSize = new Size(930, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            EmulatorUiStyle.ApplyForm(this);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 18, 14),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateBody(), 0, 1);
            root.Controls.Add(CreateFooter(), 0, 2);
            Controls.Add(root);

            sectionList.SelectedIndexChanged += (_, _) => ShowSelectedSection();
            sectionList.SelectedIndex = 0;
            KeyDown += EmulatorHelpForm_KeyDown;
        }

        private static Control CreateHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            Label title = EmulatorUiStyle.CreateTitle("CONTROLS AND SHORTCUTS");
            title.Location = new Point(0, 0);
            Label subtitle = EmulatorUiStyle.CreateSubtitle("Press F1 or Escape to close this window.");
            subtitle.Location = new Point(1, 29);
            Label reference = EmulatorUiStyle.CreateSubtitle("QUICK REFERENCE", accent: true);
            reference.AutoSize = false;
            reference.Dock = DockStyle.Right;
            reference.Width = 140;
            reference.Padding = new Padding(0, 4, 0, 0);
            reference.TextAlign = ContentAlignment.TopRight;
            panel.Controls.Add(title);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(reference);
            return panel;
        }

        private Control CreateBody()
        {
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var sectionsGroup = new GroupBox { Text = "Sections", Dock = DockStyle.Fill, Padding = new Padding(10) };
            EmulatorUiStyle.StyleGroup(sectionsGroup);
            sectionList.Dock = DockStyle.Fill;
            sectionList.BorderStyle = BorderStyle.None;
            sectionList.BackColor = EmulatorUiStyle.Paper;
            sectionList.ForeColor = EmulatorUiStyle.Ink;
            sectionList.Font = EmulatorUiStyle.UiFont(9F);
            sectionList.DrawMode = DrawMode.OwnerDrawFixed;
            sectionList.ItemHeight = 34;
            sectionList.Items.AddRange(Sections.Cast<object>().ToArray());
            sectionList.DrawItem += DrawSectionItem;
            sectionsGroup.Controls.Add(sectionList);

            detailsGroup.Dock = DockStyle.Fill;
            detailsGroup.Margin = new Padding(12, 0, 0, 0);
            detailsGroup.Padding = new Padding(12, 8, 12, 12);
            EmulatorUiStyle.StyleGroup(detailsGroup);
            detailsPanel.Dock = DockStyle.Fill;
            detailsPanel.AutoScroll = true;
            detailsGroup.Controls.Add(detailsPanel);

            body.Controls.Add(sectionsGroup, 0, 0);
            body.Controls.Add(detailsGroup, 1, 0);
            return body;
        }

        private Control CreateFooter()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 9, 0, 0)
            };
            var close = new Button { Text = "Close", Width = 90, Height = 30 };
            EmulatorUiStyle.StyleButton(close, primary: true);
            close.Click += (_, _) => Close();
            panel.Controls.Add(close);
            return panel;
        }

        private void ShowSelectedSection()
        {
            if (sectionList.SelectedItem is not HelpSection section)
                return;

            detailsGroup.Text = section.Title;
            detailsPanel.Controls.Clear();
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = section.Rows.Length,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < section.Rows.Length; row++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
                table.Controls.Add(new Label
                {
                    Text = section.Rows[row].Key,
                    Dock = DockStyle.Fill,
                    Font = EmulatorUiStyle.MonoFont(9F, FontStyle.Bold),
                    ForeColor = EmulatorUiStyle.Accent,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0)
                }, 0, row);
                table.Controls.Add(new Label
                {
                    Text = section.Rows[row].Description,
                    Dock = DockStyle.Fill,
                    Font = EmulatorUiStyle.UiFont(9F),
                    ForeColor = EmulatorUiStyle.Ink,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0)
                }, 1, row);
            }
            detailsPanel.Controls.Add(table);
        }

        private void DrawSectionItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? EmulatorUiStyle.AccentSoft : EmulatorUiStyle.Paper);
            using var foreground = new SolidBrush(selected ? EmulatorUiStyle.Accent : EmulatorUiStyle.Ink);
            e.Graphics.FillRectangle(background, e.Bounds);
            e.Graphics.DrawString(sectionList.Items[e.Index].ToString(), sectionList.Font, foreground, e.Bounds.X + 8, e.Bounds.Y + 8);
            e.DrawFocusRectangle();
        }

        private void EmulatorHelpForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode is not (Keys.F1 or Keys.Escape))
                return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            Close();
        }

        private sealed record HelpSection(string Title, (string Key, string Description)[] Rows)
        {
            public override string ToString() => Title;
        }
    }
}
