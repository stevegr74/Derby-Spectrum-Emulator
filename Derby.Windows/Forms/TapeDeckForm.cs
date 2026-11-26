using Derby.Audio;
using Derby.Tape;

namespace Derby;

internal sealed record TapeSideAction(string Label, Action? Invoke);

internal sealed class TapeDeckForm : Form
{
    private readonly Func<TapeDeckView?> read;
    private readonly Func<TapeTimeline, TapeCue?> capture;
    private readonly Func<TapeDeckView, int?, TapeCue?, bool> control;
    private readonly TapeBookmarkStore store;
    private readonly ToolTip tips = new();
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 150 };
    private readonly Label tapeName = EmulatorUiStyle.CreateSubtitle("No tape mounted");
    private readonly Label status = EmulatorUiStyle.CreateSubtitle("NO TAPE", accent: true);
    private readonly Label hint = EmulatorUiStyle.CreateSubtitle("");
    private readonly TapeTimelineStrip timeline = new();
    private readonly ListBox bookmarks = CreateList("Tape sections and bookmarks");
    private readonly TextBox name = new() { Width = 188, MaxLength = 60, PlaceholderText = "Bookmark name", AccessibleName = "Bookmark name" };
    private readonly Button choose = CreateButton("Choose tape...");
    private readonly Button tapeOptions = CreateButton("\u25BE");
    private readonly FlowLayoutPanel sideMenu = new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, Visible = false, BackColor = EmulatorUiStyle.Panel, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button toggle = CreateButton("Play tape", primary: true);
    private readonly Button rewind = CreateButton("Rewind");
    private readonly Button preview = CreateButton("Preview");
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 900 };
    private WaveOutAudioOutput? previewOutput;
    private readonly Func<TapeTimeline, short[]?>? renderPreview;
    private readonly Func<TapeTimeline, double, bool>? scrubTape;
    private readonly Button goTo = CreateButton("Go to");
    private readonly Button add = CreateButton("Add bookmark...");
    private readonly Button edit = CreateButton("Edit...");
    private readonly Button save = CreateButton("Save");
    private readonly Button remove = CreateButton("Delete");
    private readonly FlowLayoutPanel bookmarkActions;
    private readonly FlowLayoutPanel editor;
    private TapeDeckView? view;
    private TapeCue? editingCue;
    private TapeBookmarkStore.Bookmark? editingBookmark;
    private string? feedback;
    private long feedbackUntil;

    public TapeDeckForm(Func<TapeDeckView?> read, Func<TapeTimeline, TapeCue?> capture,
        Func<TapeDeckView, int?, TapeCue?, bool> control, TapeBookmarkStore store,
        Action<Form> chooseTape, Func<Form, TapeDeckView?, TapeSideAction[]>? getSideActions = null,
        Func<TapeTimeline, double, bool>? scrubTape = null, Func<TapeTimeline, short[]?>? renderPreview = null)
    {
        this.read = read;
        this.capture = capture;
        this.control = control;
        this.store = store;
        this.scrubTape = scrubTape;
        this.renderPreview = renderPreview;
        EmulatorUiStyle.ApplyForm(this);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new(96F, 96F);
        Text = "DERBY Tape Transport";
        ClientSize = new(590, 407);
        MinimumSize = new(570, 430);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;

        var root = Grid(1, 5);
        root.Padding = new(20, 16, 20, 16);
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(root);

        var header = Grid(2, 2);
        header.AutoSize = true;
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.AutoSize));
        header.Controls.Add(EmulatorUiStyle.CreateTitle("TAPE TRANSPORT"), 0, 0);
        status.Anchor = AnchorStyles.Right;
        header.Controls.Add(status, 1, 0);
        tapeName.AutoSize = false;
        tapeName.Height = 24;
        tapeName.Dock = DockStyle.Fill;
        tapeName.AutoEllipsis = true;
        header.Controls.Add(tapeName, 0, 1);
        header.SetColumnSpan(tapeName, 2);
        root.Controls.Add(header, 0, 0);

        var overview = Grid(1, 1);
        overview.AutoSize = true;
        overview.BackColor = EmulatorUiStyle.Panel;
        overview.Padding = new(12, 8, 12, 6);
        overview.Margin = new(3, 8, 3, 10);
        overview.Controls.Add(timeline, 0, 0);
        root.Controls.Add(overview, 0, 1);

        var transport = Grid(2, 1);
        transport.AutoSize = true;
        transport.ColumnStyles.Add(new(SizeType.Percent, 100));
        transport.ColumnStyles.Add(new(SizeType.AutoSize));
        transport.Controls.Add(renderPreview == null ? Row(toggle, rewind) : Row(toggle, rewind, preview), 0, 0);
        tapeOptions.AccessibleName = "Tape side options";
        tapeOptions.AutoSize = false;
        tapeOptions.MinimumSize = Size.Empty;
        tapeOptions.Size = new Size(28, choose.Height);
        tapeOptions.Margin = new Padding(0, choose.Margin.Top, choose.Margin.Right, choose.Margin.Bottom);
        choose.Margin = new Padding(choose.Margin.Left, choose.Margin.Top, 0, choose.Margin.Bottom);
        transport.Controls.Add(getSideActions == null ? Row(choose) : Row(choose, tapeOptions), 1, 0);
        root.Controls.Add(transport, 0, 2);
        Controls.Add(sideMenu);
        sideMenu.BringToFront();

        var cancelEdit = CreateButton("Cancel");
        bookmarkActions = Row(goTo, add, edit);
        editor = Row(name, save, cancelEdit, remove);
        editor.Visible = false;
        root.Controls.Add(ListPanel("Sections and bookmarks", bookmarks, bookmarkActions, editor), 0, 3);

        var footer = Grid(1, 2);
        footer.AutoSize = true;
        footer.Controls.Add(hint, 0, 0);
        var close = CreateButton("Close");
        var actions = Row(close);
        actions.AutoSize = true;
        actions.FlowDirection = FlowDirection.RightToLeft;
        footer.Controls.Add(actions, 0, 1);
        root.Controls.Add(footer, 0, 4);
        CancelButton = close;

        tips.SetToolTip(edit, "Rename or delete the selected bookmark.");
        tips.SetToolTip(tapeOptions, "Pair or switch tape sides.");
        tips.SetToolTip(preview, "Listen briefly without moving the tape or sending sound to the Spectrum.");
        choose.Click += (_, _) => { StopPreview(); sideMenu.Hide(); chooseTape(this); RefreshDeck(); };
        tapeOptions.Click += (_, _) =>
        {
            if (sideMenu.Visible)
            {
                sideMenu.Hide();
                return;
            }
            RefreshDeck();
            ShowSideOptions(getSideActions?.Invoke(this, view) ?? []);
        };
        close.Click += (_, _) => Close();
        toggle.Click += (_, _) => Run(null);
        rewind.Click += (_, _) => Run(0);
        preview.Click += (_, _) => StartPreview();
        previewTimer.Tick += (_, _) => StopPreview();
        goTo.Click += (_, _) => GoToSelection();
        add.Click += (_, _) => BeginBookmarkEdit(create: true);
        edit.Click += (_, _) => BeginBookmarkEdit(create: false);
        save.Click += (_, _) => SaveBookmark();
        cancelEdit.Click += (_, _) => EndBookmarkEdit();
        remove.Click += (_, _) => RemoveBookmark();
        bookmarks.SelectedIndexChanged += (_, _) => UpdateButtons();
        name.TextChanged += (_, _) => UpdateButtons();
        timeline.PositionSelected += SelectTimelinePosition;
        timeline.ScrubRequested += Scrub;
        Resize += (_, _) => sideMenu.Hide();
        refresh.Tick += (_, _) => RefreshDeck();
        Shown += (_, _) => { RefreshDeck(); refresh.Start(); };
        RefreshDeck();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopPreview();
            previewTimer.Dispose();
            refresh.Dispose();
            tips.Dispose();
            sideMenu.Dispose();
            tapeOptions.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (sideMenu.Visible && keyData == Keys.Escape)
        {
            sideMenu.Hide();
            tapeOptions.Focus();
            return true;
        }
        if (editingCue != null && keyData == Keys.Escape)
        {
            EndBookmarkEdit();
            return true;
        }
        if (name.Focused && keyData == Keys.Enter && save.Enabled)
        {
            SaveBookmark();
            return true;
        }
        if (bookmarks.Focused && keyData == Keys.Enter && goTo.Enabled)
        {
            GoToSelection();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    private void ShowSideOptions(TapeSideAction[] actions)
    {
        foreach (Control item in sideMenu.Controls.Cast<Control>().ToArray())
            item.Dispose();
        foreach (TapeSideAction action in actions)
        {
            var button = CreateButton(action.Label);
            button.AutoSize = false;
            button.Width = LogicalToDeviceUnits(250);
            button.Margin = new Padding(1);
            button.UseMnemonic = false;
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Enabled = action.Invoke != null;
            tips.SetToolTip(button, action.Label);
            button.Click += (_, _) =>
            {
                StopPreview();
                sideMenu.Hide();
                tapeOptions.Focus();
                action.Invoke?.Invoke();
                RefreshDeck();
            };
            sideMenu.Controls.Add(button);
        }
        Size menuSize = sideMenu.GetPreferredSize(Size.Empty);
        Point anchor = PointToClient(tapeOptions.PointToScreen(new Point(tapeOptions.Width, tapeOptions.Height)));
        sideMenu.Location = new Point(Math.Max(0, anchor.X - menuSize.Width), anchor.Y);
        sideMenu.Visible = actions.Length > 0;
        sideMenu.Controls.Cast<Control>().FirstOrDefault(item => item.Enabled)?.Focus();
    }

    private void RefreshDeck()
    {
        TapeDeckView? previous = view;
        view = read();
        if (previous?.State != view?.State || previous?.Position != view?.Position ||
            !ReferenceEquals(previous?.Timeline, view?.Timeline))
            StopPreview();
        if (!ReferenceEquals(previous?.Timeline, view?.Timeline))
        {
            sideMenu.Hide();
            tapeName.Text = view?.Timeline.FileName ?? "No tape mounted";
            tips.SetToolTip(tapeName, view?.Timeline.FileName);
            EndBookmarkEdit();
            bookmarks.ClearSelected();
        }
        if (!ReferenceEquals(previous?.Timeline, view?.Timeline) ||
            !ReferenceEquals(previous?.Markers, view?.Markers))
            FillBookmarks();

        status.Text = view?.State switch
        {
            TapeTransportState.Playing => "PLAYING",
            TapeTransportState.Stopped => "STOPPED",
            TapeTransportState.Ended => "AT END",
            _ => "NO TAPE"
        };
        hint.Text = feedback != null && Environment.TickCount64 < feedbackUntil ? feedback : view?.State switch
        {
            TapeTransportState.Playing => "Stop the tape before seeking or previewing.",
            TapeTransportState.Stopped when view.CanSeek => "Drag the bar to seek, or use Preview to listen.",
            TapeTransportState.Stopped => "Seeking is unavailable while the loader is handing off.",
            TapeTransportState.Ended => "Rewind or choose a position to play again.",
            _ => "Choose a tape to get started."
        };
        toggle.Text = view?.State == TapeTransportState.Playing ? "Stop tape" : "Play tape";
        RedrawTimeline();
        UpdateButtons();
    }

    private void FillBookmarks(object? selected = null)
    {
        selected ??= bookmarks.SelectedItem;
        bookmarks.BeginUpdate();
        bookmarks.Items.Clear();
        if (view != null)
        {
            var entries = view.Timeline.Sections
                .Select(section => new PositionEntry(section.BlockIndex, section.Name, TapeMarkKind.Section))
                .Concat(store.For(view.Timeline).Select(bookmark =>
                    new PositionEntry(bookmark.Cue.BlockIndex, bookmark.Name, TapeMarkKind.Bookmark, bookmark.Cue, bookmark)))
                .Concat(view.Markers.TakeLast(1).Select(marker =>
                    new PositionEntry(marker.BlockIndex, $"Last stop: {marker.Label}", TapeMarkKind.Stop, marker.Cue)))
                .OrderBy(entry => entry.BlockIndex).ToArray();
            bookmarks.Items.AddRange(entries);
        }
        if (selected != null && bookmarks.Items.Contains(selected))
            bookmarks.SelectedItem = selected;
        bookmarks.EndUpdate();
        timeline.UpdateMarks(bookmarks.Items.OfType<PositionEntry>().Select(entry => entry.Mark).ToArray());
        RedrawTimeline();
        UpdateButtons();
    }

    private bool CanScrub => view is { State: TapeTransportState.Stopped, CanSeek: true };

    private void RedrawTimeline() =>
        timeline.UpdatePositions(view?.Timeline.BlockCount ?? 0, view?.Position ?? 0, CanScrub && scrubTape != null);

    private void UpdateButtons()
    {
        bool canSeek = view?.CanSeek == true;
        bool stopped = view?.State == TapeTransportState.Stopped;
        bool editing = editingCue != null;
        toggle.Enabled = view?.State is TapeTransportState.Playing or TapeTransportState.Stopped;
        SetPrimary(choose, view == null);
        SetPrimary(toggle, toggle.Enabled);
        rewind.Enabled = canSeek;
        preview.Enabled = CanScrub;
        var selected = bookmarks.SelectedItem as PositionEntry;
        goTo.Enabled = canSeek && selected?.CanGoTo == true;
        add.Enabled = !editing && (stopped || selected is { Kind: TapeMarkKind.Stop, Cue: not null });
        edit.Enabled = !editing && selected?.Bookmark != null;
        save.Enabled = editing && !string.IsNullOrWhiteSpace(name.Text);
    }

    private void GoToSelection()
    {
        if (goTo.Enabled && bookmarks.SelectedItem is PositionEntry entry)
            Run(entry.BlockIndex, entry.Cue);
    }

    private void Run(int? index, TapeCue? cue = null)
    {
        StopPreview();
        sideMenu.Hide();
        bool executed = view != null && control(view, index, cue);
        feedback = executed ? null : "Could not move to that position. Check the tape and try again.";
        feedbackUntil = Environment.TickCount64 + 3500;
        RefreshDeck();
    }

    private void Scrub(double position)
    {
        StopPreview();
        sideMenu.Hide();
        if (CanScrub && scrubTape != null)
        {
            feedback = scrubTape(view!.Timeline, position) ? null : "No seekable tape signal at that position.";
            feedbackUntil = Environment.TickCount64 + 3500;
        }
        RefreshDeck();
    }

    private void StartPreview()
    {
        StopPreview();
        if (!CanScrub || renderPreview == null)
            return;
        short[]? samples = renderPreview(view!.Timeline);
        if (samples == null)
        {
            RefreshDeck();
            return;
        }
        try
        {
            previewOutput = new WaveOutAudioOutput();
            previewOutput.WriteSamples(samples);
            previewTimer.Start();
            feedback = Array.Exists(samples, sample => sample != 0) ? "Preview only. Tape stays stopped." : "No audible signal here.";
        }
        catch (InvalidOperationException)
        {
            StopPreview();
            feedback = "Tape preview audio is unavailable.";
        }
        feedbackUntil = Environment.TickCount64 + 2500;
        RefreshDeck();
    }

    private void StopPreview()
    {
        previewTimer.Stop();
        previewOutput?.Dispose();
        previewOutput = null;
    }

    private void SelectTimelinePosition(TapeMark mark)
    {
        for (int i = 0; i < bookmarks.Items.Count; i++)
        {
            if (bookmarks.Items[i] is not PositionEntry entry || entry.Mark != mark)
                continue;
            bookmarks.SelectedIndex = i;
            bookmarks.Focus();
            return;
        }
    }

    private void BeginBookmarkEdit(bool create)
    {
        if (view == null)
            return;
        var selected = bookmarks.SelectedItem as PositionEntry;
        TapeCue? cue = create
            ? (selected?.Kind == TapeMarkKind.Stop ? selected.Cue : null) ?? capture(view.Timeline)
            : selected?.Bookmark?.Cue;
        if (cue == null)
        {
            RefreshDeck();
            return;
        }
        editingBookmark = create ? null : selected?.Bookmark;
        editingCue = cue;
        name.Text = editingBookmark?.Name ?? "";
        remove.Visible = editingBookmark != null;
        bookmarkActions.Visible = false;
        editor.Visible = true;
        UpdateButtons();
        name.Focus();
        name.SelectAll();
    }

    private void SaveBookmark()
    {
        if (view == null || editingCue == null || string.IsNullOrWhiteSpace(name.Text))
            return;
        var bookmark = new TapeBookmarkStore.Bookmark(editingBookmark?.Id ?? Guid.NewGuid(), name.Text.Trim(), editingCue);
        if (!store.TryChange(view.Timeline, editingBookmark, bookmark))
        {
            ShowBookmarkWriteError();
            return;
        }
        feedback = null;
        EndBookmarkEdit();
        FillBookmarks(new PositionEntry(bookmark.Cue.BlockIndex, bookmark.Name, TapeMarkKind.Bookmark, bookmark.Cue, bookmark));
    }

    private void RemoveBookmark()
    {
        if (view == null || editingBookmark == null)
            return;
        if (!store.TryChange(view.Timeline, editingBookmark, null))
        {
            ShowBookmarkWriteError();
            return;
        }
        feedback = null;
        EndBookmarkEdit();
        FillBookmarks();
    }

    private void ShowBookmarkWriteError()
    {
        feedback = "Could not update saved bookmarks.";
        feedbackUntil = Environment.TickCount64 + 5000;
        RefreshDeck();
    }

    private void EndBookmarkEdit()
    {
        editingCue = null;
        editingBookmark = null;
        editor.Visible = false;
        bookmarkActions.Visible = true;
        name.Clear();
        UpdateButtons();
    }

    private static TableLayoutPanel Grid(int columns, int rows)
        => new() { ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill };

    private static ListBox CreateList(string label)
        => new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = label };

    private static Button CreateButton(string label, bool primary = false)
    {
        var button = new Button { Text = label, AutoSize = true, MinimumSize = new(58, 32), Margin = new(0, 3, 6, 3) };
        EmulatorUiStyle.StyleButton(button, primary);
        return button;
    }

    private static void SetPrimary(Button button, bool primary)
    {
        button.BackColor = primary ? EmulatorUiStyle.Accent : EmulatorUiStyle.Field;
        button.ForeColor = primary ? Color.White : EmulatorUiStyle.Ink;
        button.FlatAppearance.BorderColor = primary ? EmulatorUiStyle.Accent : EmulatorUiStyle.Border;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new(3, 0, 0, 0) };
        row.Controls.AddRange(controls);
        return row;
    }

    private static TableLayoutPanel ListPanel(string title, Control list, params Control[] actions)
    {
        var panel = Grid(1, 2 + actions.Length);
        panel.RowStyles.Add(new(SizeType.AutoSize));
        panel.RowStyles.Add(new(SizeType.Percent, 100));
        panel.Controls.Add(EmulatorUiStyle.CreateSectionLabel(title), 0, 0);
        panel.Controls.Add(list, 0, 1);
        for (int i = 0; i < actions.Length; i++)
        {
            panel.RowStyles.Add(new(SizeType.AutoSize));
            panel.Controls.Add(actions[i], 0, i + 2);
        }
        return panel;
    }

    private sealed record PositionEntry(int BlockIndex, string Name, TapeMarkKind Kind, TapeCue? Cue = null,
        TapeBookmarkStore.Bookmark? Bookmark = null)
    {
        public TapeMark Mark => new(BlockIndex, Kind);
        public bool CanGoTo => Kind == TapeMarkKind.Section || Cue != null;
        public override string ToString() => Kind == TapeMarkKind.Section ? $"Section: {Name}" : Name;
    }
}
