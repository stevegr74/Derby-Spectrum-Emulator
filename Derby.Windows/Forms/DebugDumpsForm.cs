using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Derby;

internal sealed class DebugDumpsForm : Form
{
    private readonly string folder;
    private readonly Action<string> openDump;
    private readonly Action<string> showInFolder;
    private readonly ListView dumps = new()
    {
        AccessibleName = "Saved debug dumps", Dock = DockStyle.Fill, View = View.Details,
        FullRowSelect = true, MultiSelect = false, HideSelection = false
    };
    private readonly Label feedback = EmulatorUiStyle.CreateSubtitle(string.Empty);
    private readonly Button open = CreateButton("Open Dump", primary: true);
    private readonly Button reveal = CreateButton("Show in Folder");

    internal DebugDumpsForm(string folder, Action<string>? openDump = null, Action<string>? showInFolder = null)
    {
        this.folder = folder;
        this.openDump = openDump ?? OpenDump;
        this.showInFolder = showInFolder ?? ShowInFolder;

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        EmulatorUiStyle.ApplyForm(this);
        Text = "DERBY Debug Dumps";
        ClientSize = new Size(670, 370);
        MinimumSize = new Size(620, 285);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(EmulatorUiStyle.CreateTitle("DEBUG DUMPS"), 0, 0);
        root.Controls.Add(EmulatorUiStyle.CreateSubtitle("Saved machine diagnostics and crash reports"), 0, 1);

        dumps.BackColor = EmulatorUiStyle.Field;
        dumps.ForeColor = EmulatorUiStyle.Ink;
        dumps.Columns.Add("Filename", 340);
        dumps.Columns.Add("Modified", 166);
        dumps.Columns.Add("Type", 90);
        dumps.SizeChanged += (_, _) => FitFilenameColumn();
        dumps.SelectedIndexChanged += (_, _) => UpdateActions();
        dumps.ItemActivate += (_, _) => RunSelectedAction(this.openDump);
        root.Controls.Add(dumps, 0, 2);
        feedback.AutoSize = false;
        feedback.AutoEllipsis = true;
        feedback.Dock = DockStyle.Fill;
        feedback.Height = 22;
        root.Controls.Add(feedback, 0, 3);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        var close = CreateButton("Close");
        var refresh = CreateButton("Refresh");
        close.Click += (_, _) => Close();
        refresh.Click += (_, _) => RefreshDumps(selectLatest: false);
        open.Click += (_, _) => RunSelectedAction(this.openDump);
        reveal.Click += (_, _) => RunSelectedAction(this.showInFolder);
        buttons.Controls.AddRange([close, reveal, open, refresh]);
        root.Controls.Add(buttons, 0, 4);
        CancelButton = close;

        Shown += (_, _) =>
        {
            FitFilenameColumn();
            if (dumps.Items.Count != 0)
                dumps.Focus();
        };
        RefreshDumps(selectLatest: true);
    }

    internal void RefreshDumps(bool selectLatest)
    {
        string? previousPath = selectLatest ? null : SelectedPath();
        dumps.BeginUpdate();
        try
        {
            dumps.Items.Clear();
            var directory = new DirectoryInfo(folder);
            if (directory.Exists)
            {
                foreach (FileInfo file in directory.EnumerateFiles("*.txt")
                    .Where(file => IsDumpName(file.Name))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase))
                {
                    dumps.Items.Add(new ListViewItem([file.Name,
                        file.LastWriteTime.ToString("g", CultureInfo.CurrentCulture), GetDumpType(file)])
                    {
                        Tag = file.FullName
                    });
                }
            }
            feedback.Text = dumps.Items.Count == 0 ? "No debug dumps yet. Press F12 in the emulator to save one." : folder;
            if (dumps.Items.Count != 0)
            {
                ListViewItem item = dumps.Items.Cast<ListViewItem>()
                    .FirstOrDefault(row => string.Equals((string?)row.Tag, previousPath, StringComparison.OrdinalIgnoreCase))
                    ?? dumps.Items[0];
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            feedback.Text = $"Could not list debug dumps: {ex.Message}";
        }
        finally
        {
            dumps.EndUpdate();
            UpdateActions();
        }
    }

    private string? SelectedPath() => dumps.SelectedItems.Count == 1 ? dumps.SelectedItems[0].Tag as string : null;

    private void UpdateActions() => open.Enabled = reveal.Enabled = SelectedPath() != null;

    private void FitFilenameColumn() => dumps.Columns[0].Width = Math.Max(80,
        dumps.ClientSize.Width - dumps.Columns[1].Width - dumps.Columns[2].Width - SystemInformation.VerticalScrollBarWidth - 3);

    private void RunSelectedAction(Action<string> action)
    {
        if (SelectedPath() is not { } path)
            return;
        if (!File.Exists(path))
        {
            RefreshDumps(selectLatest: false);
            feedback.Text = "That dump is no longer available.";
            return;
        }

        try
        {
            action(path);
            feedback.Text = folder;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            feedback.Text = $"Could not open the dump: {ex.Message}";
        }
    }

    private static bool IsDumpName(string name) =>
        name.StartsWith("machine-debug-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("crash-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("emulation-loop-crash-", StringComparison.OrdinalIgnoreCase);

    private static string GetDumpType(FileInfo file)
    {
        if (!file.Name.StartsWith("machine-debug-", StringComparison.OrdinalIgnoreCase))
            return "Crash";
        if (file.Name.StartsWith("machine-debug-auto-", StringComparison.OrdinalIgnoreCase))
            return "Automatic";

        // Older manual and automatic dumps used the same filename prefix.
        try
        {
            using var reader = new StreamReader(file.FullName);
            if (reader.ReadLine() == "=== REASON ===")
                return reader.ReadLine() == "Manual F12 debug dump." ? "Manual" : "Automatic";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return "Machine";
    }

    private static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(88, 30) };
        EmulatorUiStyle.StyleButton(button, primary);
        return button;
    }

    private static void OpenDump(string path)
    {
        using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static void ShowInFolder(string path)
    {
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        using Process? process = Process.Start(new ProcessStartInfo(explorer, $"/select,\"{path}\""));
    }
}
