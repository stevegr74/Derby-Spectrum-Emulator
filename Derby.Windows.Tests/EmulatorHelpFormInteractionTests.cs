using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests
{
    public class EmulatorHelpFormInteractionTests
    {
        [Fact]
        public void Form_ShowsEverySectionWithoutClippingShortcutText()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new EmulatorHelpForm();
                    form.Show();
                    Application.DoEvents();

                    Assert.Equal(Color.FromArgb(246, 246, 242), form.BackColor);
                    Assert.Equal("Segoe UI", form.Font.Name);
                    Assert.True(form.ClientSize.Width >= 930);

                    ListBox sections = Descendants(form).OfType<ListBox>().Single();
                    Assert.Equal(
                        ["Main shortcuts", "Tape and disk", "Debugger window", "Spectrum keyboard",
                            "Joystick", "Mouse", "Poke Manager"],
                        sections.Items.Cast<object>().Select(section => section.ToString()));
                    GroupBox details = Descendants(form).OfType<GroupBox>()
                        .Single(group => group.Text != "Sections");
                    Assert.Equal("Main shortcuts", details.Text);
                    var shortcutRows = Descendants(details).OfType<TableLayoutPanel>().Single();
                    Assert.Equal(
                        ["F1", "F2", "F3", "F4", "Shift+F4", "F5", "Shift+F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Shift+F12", "Shift+F10"],
                        Enumerable.Range(0, shortcutRows.RowCount)
                            .Select(row => shortcutRows.GetControlFromPosition(0, row)!.Text));
                    Assert.True(shortcutRows.Parent!.ClientRectangle.Contains(shortcutRows.Bounds),
                        "The complete main shortcut list should fit without scrolling.");

                    for (int index = 0; index < sections.Items.Count; index++)
                    {
                        sections.SelectedIndex = index;
                        Application.DoEvents();
                        if (details.Text == "Debugger window")
                            Assert.Contains(Descendants(details).OfType<Label>(), label => label.Text == "Esc");
                        foreach (Label label in Descendants(details).OfType<Label>())
                        {
                            int textWidth = TextRenderer.MeasureText(label.Text, label.Font).Width;
                            Assert.True(
                                textWidth <= label.ClientSize.Width - label.Padding.Horizontal,
                                $"Help text is clipped in '{details.Text}': {label.Text}");
                        }
                    }

                    Button close = Descendants(form).OfType<Button>().Single(button => button.Text == "Close");
                    Assert.Equal(Color.FromArgb(8, 121, 101), close.BackColor);
                    form.Close();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "The Help form interaction test timed out.");
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child))
                    yield return descendant;
            }
        }
    }
}
