using System.Runtime.InteropServices;

namespace MicDJ;

internal sealed partial class MainForm
{
    private (Button Add, Button Remove) BuildStudioLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.Controls.Add(new BrandHeader { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        root.Controls.Add(body, 0, 1);

        var devices = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = StudioTheme.Panel, Padding = new Padding(20, 20, 16, 16), Margin = Padding.Empty, ColumnCount = 1, RowCount = 8 };
        devices.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        devices.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        for (int i = 0; i < 4; i++) devices.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        devices.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        devices.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        devices.Controls.Add(Heading("Devices"), 0, 0);
        devices.Controls.Add(new Label { Text = "Select the audio devices used by MicDJ.", Dock = DockStyle.Fill, ForeColor = StudioTheme.Muted, Margin = Padding.Empty }, 0, 1);
        AddDevice(devices, 2, "Physical microphone", microphone);
        AddDevice(devices, 3, "Mix output", destination);
        AddDevice(devices, 4, "Virtual input", virtualInput);
        AddDevice(devices, 5, "Headphones / monitor", headphones);
        refresh.Dock = DockStyle.Fill;
        devices.Controls.Add(refresh, 0, 6);
        body.Controls.Add(devices, 0, 0);

        var library = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 20, 20, 20), ColumnCount = 1, RowCount = 4 };
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        library.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        var toggles = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
        micToggle.Dock = monitorToggle.Dock = DockStyle.Fill;
        micToggle.Margin = monitorToggle.Margin = new Padding(0, 0, 0, 20);
        toggles.Controls.Add(micToggle, 0, 0);
        toggles.Controls.Add(monitorToggle, 1, 0);
        library.Controls.Add(toggles, 0, 0);
        library.Controls.Add(Heading("Sound library"), 0, 1);
        var listFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = StudioTheme.Border, Margin = Padding.Empty };
        listFrame.Controls.Add(sounds);
        library.Controls.Add(listFrame, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), ColumnCount = 3, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        var add = NewButton("+  Add WAV");
        var remove = NewButton("Remove from list");
        Button[] buttons = [add, play, remove];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Dock = DockStyle.Fill;
            buttons[i].Margin = new Padding(i == 0 ? 0 : 7, 0, 0, 0);
            actions.Controls.Add(buttons[i], i, 0);
        }
        library.Controls.Add(actions, 0, 3);
        body.Controls.Add(library, 1, 0);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 22, 20, 22), ColumnCount = 3, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        status.Dock = DockStyle.Fill;
        status.AutoSize = false;
        status.Padding = Padding.Empty;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.ForeColor = StudioTheme.Text;
        start.Dock = stop.Dock = DockStyle.Fill;
        start.Margin = new Padding(8, 0, 0, 0);
        stop.Margin = new Padding(8, 0, 0, 0);
        footer.Controls.Add(status, 0, 0);
        footer.Controls.Add(start, 1, 0);
        footer.Controls.Add(stop, 2, 0);
        footer.Paint += (_, e) => e.Graphics.DrawLine(Pens.DimGray, 20, 0, footer.Width - 20, 0);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "MicDJ.ico"));
        return (add, remove);
    }

    private static Label Heading(string text) => new() { Text = text, Font = new Font("Bahnschrift", 18, FontStyle.Bold), Dock = DockStyle.Fill, Margin = Padding.Empty };

    private static void AddDevice(TableLayoutPanel parent, int row, string text, ComboBox combo)
    {
        var group = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = 2 };
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        group.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty, Font = new Font("Bahnschrift", 10, FontStyle.Bold) }, 0, 0);
        combo.AccessibleName = text;
        combo.Margin = new Padding(0, 0, 0, 18);
        group.Controls.Add(combo, 0, 1);
        parent.Controls.Add(group, 0, row);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
