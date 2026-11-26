using Derby.Tape;

namespace Derby;

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
    private readonly Button toggle = CreateButton("Play tape", primary: true);
    private readonly Button rewind = CreateButton("Rewind");
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
        Action<Form> chooseTape)
    {
        this.read = read;
        this.capture = capture;
        this.control = control;
        this.store = store;
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
        transport.Controls.Add(Row(toggle, rewind), 0, 0);
        transport.Controls.Add(Row(choose), 1, 0);
        root.Controls.Add(transport, 0, 2);

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
        choose.Click += (_, _) => { chooseTape(this); RefreshDeck(); };
        close.Click += (_, _) => Close();
        toggle.Click += (_, _) => Run(null);
        rewind.Click += (_, _) => Run(0);
        goTo.Click += (_, _) => GoToSelection();
        add.Click += (_, _) => BeginBookmarkEdit(create: true);
        edit.Click += (_, _) => BeginBookmarkEdit(create: false);
        save.Click += (_, _) => SaveBookmark();
        cancelEdit.Click += (_, _) => EndBookmarkEdit();
        remove.Click += (_, _) => RemoveBookmark();
        bookmarks.SelectedIndexChanged += (_, _) => UpdateButtons();
        name.TextChanged += (_, _) => UpdateButtons();
        timeline.PositionSelected += SelectTimelinePosition;
        refresh.Tick += (_, _) => RefreshDeck();
        Shown += (_, _) => { RefreshDeck(); refresh.Start(); };
        RefreshDeck();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refresh.Dispose();
            tips.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
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

    private void RefreshDeck()
    {
        TapeDeckView? previous = view;
        view = read();
        if (!ReferenceEquals(previous?.Timeline, view?.Timeline))
        {
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
            TapeTransportState.Playing => "Stop the tape before jumping to a section or bookmark.",
            TapeTransportState.Stopped when view.CanSeek => "Bookmarks are saved for this tape image.",
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

    private void RedrawTimeline() => timeline.UpdatePositions(view?.Timeline.BlockCount ?? 0, view?.BlockIndex ?? 0);

    private void UpdateButtons()
    {
        bool canSeek = view?.CanSeek == true;
        bool stopped = view?.State == TapeTransportState.Stopped;
        bool editing = editingCue != null;
        toggle.Enabled = view?.State is TapeTransportState.Playing or TapeTransportState.Stopped;
        SetPrimary(choose, view == null);
        SetPrimary(toggle, toggle.Enabled);
        rewind.Enabled = canSeek;
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
        bool executed = view != null && control(view, index, cue);
        feedback = executed ? null : "Could not move to that position. Check the tape and try again.";
        feedbackUntil = Environment.TickCount64 + 3500;
        RefreshDeck();
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
