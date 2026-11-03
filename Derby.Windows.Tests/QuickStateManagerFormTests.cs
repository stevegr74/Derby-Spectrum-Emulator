using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace Derby.Tests
{
    public sealed class QuickStateManagerFormTests
    {
        [Fact]
        public void Form_PresentsEightSlotsWithoutScrollingAndUpdatesSavedSlotMetadata()
        {
            RunSta(() =>
            {
                SpectrumRomSet romSet = SpectrumRomSet.Bundled128K(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                var machine = new SpectrumMachine(romSet);
                var slots = new QuickStateSlot?[8];
                using var form = new QuickStateManagerForm(
                    index => slots[index],
                    index => slots[index] = CreateSlot(machine, romSet, $"Game {index + 1}.tzx"),
                    index => slots[index] != null,
                    index => DeleteSlot(slots, index),
                    _ => true,
                    _ => true);

                form.Show();
                Application.DoEvents();

                Assert.Equal(FormBorderStyle.FixedDialog, form.FormBorderStyle);
                Assert.False(form.MaximizeBox);
                Assert.False(form.MinimizeBox);
                GroupBox[] groups = Descendants(form).OfType<GroupBox>()
                    .Where(group => group.Name.StartsWith("QuickStateSlot", StringComparison.Ordinal))
                    .OrderBy(group => group.Name)
                    .ToArray();
                Assert.Equal(8, groups.Length);
                Assert.All(groups, group => Assert.True(group.Bounds.Bottom <= group.Parent!.ClientRectangle.Bottom));
                Assert.False(Find<Panel>(form, "QuickStateSlotScroll").AutoScroll);
                Assert.All(Enumerable.Range(1, 8), slot =>
                {
                    Button load = Find<Button>(form, $"QuickStateLoad{slot}");
                    Assert.False(load.Enabled);
                    Button save = Find<Button>(form, $"QuickStateSave{slot}");
                    Assert.Equal("Save", save.Text);
                    Assert.Equal(save.Width, load.Width);
                    Assert.True(
                        load.Parent!.ClientSize.Height - load.Bottom >= 4,
                        $"Slot {slot} Load bottom clearance was {load.Parent.ClientSize.Height - load.Bottom}px.");
                    Assert.True(
                        save.Parent!.ClientSize.Height - save.Bottom >= 4,
                        $"Slot {slot} Save bottom clearance was {save.Parent.ClientSize.Height - save.Bottom}px.");
                    Assert.Equal(EmulatorUiStyle.Accent, save.BackColor);
                    Assert.Equal(EmulatorUiStyle.Field, load.BackColor);
                    Assert.False(Find<Button>(form, $"QuickStateDelete{slot}").Visible);
                });

                Find<Button>(form, "QuickStateSave1").PerformClick();
                Application.DoEvents();

                Assert.NotNull(slots[0]);
                Assert.Equal(new Size(160, 120), slots[0]!.Thumbnail.Size);
                Assert.Equal("Game 1.tzx", Find<Label>(form, "QuickStateMedia1").Text);
                Assert.Contains("128K", Find<Label>(form, "QuickStateMachine1").Text);
                Assert.Equal("Replace", Find<Button>(form, "QuickStateSave1").Text);
                Assert.Equal(EmulatorUiStyle.Field, Find<Button>(form, "QuickStateSave1").BackColor);
                Assert.True(Find<Button>(form, "QuickStateLoad1").Enabled);
                Assert.Equal(EmulatorUiStyle.Accent, Find<Button>(form, "QuickStateLoad1").BackColor);
                Assert.True(Find<Button>(form, "QuickStateDelete1").Visible);

                Find<Button>(form, "QuickStateDelete1").PerformClick();
                Application.DoEvents();
                Assert.Null(slots[0]);
                Assert.False(Find<Button>(form, "QuickStateDelete1").Visible);
                Assert.False(Find<Button>(form, "QuickStateLoad1").Enabled);

                form.Close();
                Application.DoEvents();
                foreach (QuickStateSlot? slot in slots)
                    slot?.Dispose();
            });
        }

        [Fact]
        public void ReplaceRequiresConfirmationAndLoadUsesSelectedSlot()
        {
            RunSta(() =>
            {
                SpectrumRomSet romSet = SpectrumRomSet.Bundled128K(Path.Combine(AppContext.BaseDirectory, "ROMs"));
                var machine = new SpectrumMachine(romSet);
                var slots = new QuickStateSlot?[8];
                slots[3] = CreateSlot(machine, romSet, "Existing.sna");
                int saveCalls = 0;
                int deleteCalls = 0;
                int loadedIndex = -1;
                using var form = new QuickStateManagerForm(
                    index => slots[index],
                    index =>
                    {
                        saveCalls++;
                        return slots[index] = CreateSlot(machine, romSet, "Replacement.sna");
                    },
                    index =>
                    {
                        loadedIndex = index;
                        return true;
                    },
                    index =>
                    {
                        deleteCalls++;
                        return DeleteSlot(slots, index);
                    },
                    _ => false,
                    _ => false);

                form.Show();
                Application.DoEvents();
                Find<Button>(form, "QuickStateSave4").PerformClick();
                Assert.Equal(0, saveCalls);
                Assert.Equal("Existing.sna", Find<Label>(form, "QuickStateMedia4").Text);

                Find<Button>(form, "QuickStateDelete4").PerformClick();
                Assert.Equal(0, deleteCalls);
                Assert.NotNull(slots[3]);

                Find<Button>(form, "QuickStateLoad4").PerformClick();
                Application.DoEvents();
                Assert.Equal(3, loadedIndex);
                Assert.True(form.IsDisposed || !form.Visible);

                foreach (QuickStateSlot? slot in slots)
                    slot?.Dispose();
            });
        }

        private static QuickStateSlot CreateSlot(
            SpectrumMachine machine,
            SpectrumRomSet romSet,
            string mediaName)
        {
            var thumbnail = new Bitmap(160, 120);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
                graphics.Clear(Color.Black);
            return new QuickStateSlot(
                machine.CaptureQuickState(),
                machine.CapturePersistentState(),
                romSet,
                mediaName,
                null,
                thumbnail);
        }

        private static bool DeleteSlot(QuickStateSlot?[] slots, int index)
        {
            QuickStateSlot? slot = slots[index];
            if (slot == null)
                return false;
            slots[index] = null;
            slot.Dispose();
            return true;
        }

        private static T Find<T>(Control root, string name) where T : Control =>
            Descendants(root).OfType<T>().Single(control => control.Name == name);

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child))
                    yield return descendant;
            }
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The Quick State Manager test timed out.");
            if (failure != null)
                throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
