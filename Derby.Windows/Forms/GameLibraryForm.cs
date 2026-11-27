namespace Derby;

internal sealed class GameLibraryForm : Form
{
    private readonly GameLibrary library;
    private readonly Label folder = new() { AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label selection = new() { Name = "LibrarySelection", AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TextBox search = new() { Name = "LibrarySearch", PlaceholderText = "Search by title or filename", Dock = DockStyle.Fill };
    private readonly Button all = new() { Name = "LibraryAll", Text = "All games", AutoSize = true };
    private readonly Button recent = new() { Name = "LibraryRecent", Text = "Recent games", AutoSize = true };
    private readonly Button open = new() { Name = "LibraryOpen", Text = "Open", AutoSize = true, MinimumSize = new Size(120, 32) };
    private readonly ListView list = new()
    {
        Name = "LibraryList", AccessibleName = "Games", View = View.Details, Dock = DockStyle.Fill,
        FullRowSelect = true, MultiSelect = false, HideSelection = false, ShowItemToolTips = true,
        UseCompatibleStateImageBehavior = false
    };
    private CancellationTokenSource? scan;
    private IReadOnlyList<GameLibraryEntry> entries = [];
    private bool recentView;
    private string? scanStatus;

    public GameLibraryForm(GameLibrary library)
    {
        this.library = library;
        Text = "DERBY Game Library";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(660, 440);
        MinimumSize = new Size(540, 360);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        EmulatorUiStyle.ApplyForm(this);
        folder.ForeColor = selection.ForeColor = EmulatorUiStyle.Muted;
        list.BackColor = EmulatorUiStyle.Field;
        list.Columns.Add("Title / release");
        list.Columns.Add("Format");
        list.Columns.Add("Last opened");
        search.AccessibleName = "Search games";

        var change = new Button { Text = "Change folder...", AutoSize = true };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 32) };
        foreach (Button button in new[] { change, close, all, recent })
            EmulatorUiStyle.StyleButton(button);
        EmulatorUiStyle.StyleButton(open, primary: true);
        CancelButton = close;

        var modes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        modes.Controls.AddRange([all, recent]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < layout.RowCount; row++)
            layout.RowStyles.Add(new RowStyle(row == 5 ? SizeType.Percent : SizeType.AutoSize, row == 5 ? 100 : 0));
        layout.Controls.Add(EmulatorUiStyle.CreateTitle("GAME LIBRARY"), 0, 0);
        layout.Controls.Add(BuildRow(folder, change), 0, 1);
        layout.Controls.Add(modes, 0, 2);
        layout.Controls.Add(EmulatorUiStyle.CreateSectionLabel("Search games"), 0, 3);
        layout.Controls.Add(search, 0, 4);
        layout.Controls.Add(list, 0, 5);
        layout.Controls.Add(BuildRow(selection, close, open), 0, 6);
        Controls.Add(layout);
        ActiveControl = search;

        all.Click += (_, _) => SetView(showRecent: false);
        recent.Click += (_, _) => SetView(showRecent: true);
        change.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "Choose the Game Library folder", ShowNewFolderButton = false };
            if (Directory.Exists(library.Folder))
                picker.SelectedPath = library.Folder!;
            if (picker.ShowDialog(this) != DialogResult.OK)
                return;
            library.SetFolder(picker.SelectedPath);
            search.Clear();
            SetView(showRecent: false);
            _ = RefreshFolderAsync();
        };
        search.TextChanged += (_, _) => RefreshList();
        list.SelectedIndexChanged += (_, _) => UpdateSelection();
        list.SizeChanged += (_, _) => ResizeColumns();
        list.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && list.GetItemAt(e.X, e.Y) != null)
                AcceptSelection();
        };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            AcceptSelection();
        };
        open.Click += (_, _) => AcceptSelection();
        Shown += async (_, _) => await RefreshFolderAsync();
        FormClosed += (_, _) => scan?.Cancel();
        SetView(showRecent: false);
    }

    public GameLibraryEntry? SelectedMedia { get; private set; }

    private static TableLayoutPanel BuildRow(params Control[] controls)
    {
        var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = controls.Length, RowCount = 1 };
        for (int index = 0; index < controls.Length; index++)
        {
            row.ColumnStyles.Add(new ColumnStyle(index == 0 ? SizeType.Percent : SizeType.AutoSize, index == 0 ? 100 : 0));
            row.Controls.Add(controls[index], index, 0);
        }
        return row;
    }

    private async Task RefreshFolderAsync()
    {
        scan?.Cancel();
        string? folderPath = library.Folder;
        folder.Text = folderPath == null ? "No library folder selected" : new DirectoryInfo(folderPath).Name;
        folder.AccessibleDescription = folderPath;
        entries = [];
        scanStatus = library.Folder == null ? "Choose a folder to see your games." : "Reading folder...";
        RefreshList();
        if (library.Folder is not string path)
            return;

        using var request = new CancellationTokenSource();
        scan = request;
        try
        {
            GameLibraryEntry[] loaded = await Task.Run(() => GameLibrary.ScanFolder(path, request.Token));
            if (request.IsCancellationRequested || IsDisposed)
                return;
            entries = loaded;
            scanStatus = loaded.Length == 0 ? "No supported files in this folder." : null;
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (request.IsCancellationRequested || IsDisposed)
                return;
            scanStatus = "Cannot read this folder. Choose another folder or open Recent games.";
        }
        finally
        {
            if (scan == request)
                scan = null;
        }
        RefreshList();
    }

    private void SetView(bool showRecent)
    {
        recentView = showRecent;
        all.BackColor = showRecent ? EmulatorUiStyle.Field : EmulatorUiStyle.AccentSoft;
        recent.BackColor = showRecent ? EmulatorUiStyle.AccentSoft : EmulatorUiStyle.Field;
        ResizeColumns();
        RefreshList();
    }

    private void ResizeColumns()
    {
        int unit = DeviceDpi;
        int dateWidth = recentView ? 155 * unit / 96 : 0;
        int formatWidth = 85 * unit / 96;
        list.Columns[2].Width = dateWidth;
        list.Columns[1].Width = formatWidth;
        list.Columns[0].Width = Math.Max(60, list.ClientSize.Width - dateWidth - formatWidth - SystemInformation.VerticalScrollBarWidth - 3);
    }

    private void RefreshList()
    {
        string? previous = SelectedEntry()?.Path;
        string[] searchWords = GameLibrary.SearchWords(search.Text);
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (GameLibraryEntry entry in recentView ? library.Recent : entries)
            {
                if (!entry.Matches(searchWords))
                    continue;
                bool missing = recentView && !File.Exists(entry.Path);
                var row = new ListViewItem(missing ? $"{entry.Title} (missing)" : entry.Title) { Tag = entry, ToolTipText = entry.Path };
                row.SubItems.Add(entry.Format);
                row.SubItems.Add(entry.LastOpened?.ToLocalTime().ToString("g") ?? string.Empty);
                list.Items.Add(row);
                if (string.Equals(previous, entry.Path, StringComparison.OrdinalIgnoreCase))
                    row.Selected = true;
            }
            if (list.SelectedItems.Count == 0 && list.Items.Count > 0)
                list.Items[0].Selected = true;
        }
        finally { list.EndUpdate(); }
        UpdateSelection();
    }

    private GameLibraryEntry? SelectedEntry() =>
        list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as GameLibraryEntry;

    private void UpdateSelection()
    {
        GameLibraryEntry? entry = SelectedEntry();
        bool exists = entry != null && File.Exists(entry.Path);
        open.Enabled = exists;
        open.Text = entry?.Kind switch
        {
            GameMediaKind.Tape => "Load tape",
            GameMediaKind.Snapshot => "Load snapshot",
            GameMediaKind.Recording => "Play recording",
            GameMediaKind.Disk => "Open Disk Drive",
            _ => "Open"
        };
        selection.Text = library.SaveError ?? (entry == null
            ? recentView ? library.Recent.Count == 0 ? "No recently opened games." : "No matching recent games." : scanStatus ?? "No matching games."
            : exists ? entry.FileName : $"File not found: {entry.FileName}");
    }

    private void AcceptSelection()
    {
        if (SelectedEntry() is not { } entry || !File.Exists(entry.Path))
        {
            RefreshList();
            return;
        }
        SelectedMedia = entry;
        DialogResult = DialogResult.OK;
        Close();
    }
}
