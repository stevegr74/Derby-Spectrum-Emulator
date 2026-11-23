namespace Derby;

internal enum TapeFileAction
{
    MountStopped,
    StartNewGame
}

internal sealed class TapeFileActionForm : Form
{
    internal TapeFileAction Action { get; private set; } = TapeFileAction.MountStopped;

    internal TapeFileActionForm(string fileName)
    {
        EmulatorUiStyle.ApplyForm(this);
        AutoScaleDimensions = new(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "DERBY Choose Tape";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new(440, 164);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18, 12, 18, 12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Absolute, 25));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(EmulatorUiStyle.CreateTitle("CHOOSE TAPE"), 0, 0);
        var file = EmulatorUiStyle.CreateSubtitle(fileName);
        file.AutoSize = false;
        file.Dock = DockStyle.Fill;
        file.AutoEllipsis = true;
        layout.Controls.Add(file, 0, 1);
        layout.Controls.Add(EmulatorUiStyle.CreateSubtitle(
            "Change tape keeps the running game.\nStarting a new game resets the Spectrum."), 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var change = AddButton("Change tape", DialogResult.OK, primary: true);
        var start = AddButton("Start new game", DialogResult.OK);
        var cancel = AddButton("Cancel", DialogResult.Cancel);
        start.Click += (_, _) => Action = TapeFileAction.StartNewGame;
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = change;
        CancelButton = cancel;
        ActiveControl = change;

        Button AddButton(string caption, DialogResult result, bool primary = false)
        {
            var button = new Button { Text = caption, AutoSize = true, MinimumSize = new(80, 32), DialogResult = result };
            EmulatorUiStyle.StyleButton(button, primary);
            buttons.Controls.Add(button);
            return button;
        }
    }
}
