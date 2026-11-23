using MicDJ.Audio;
using MicDJ.Core;
using NAudio.CoreAudioApi;

namespace MicDJ;

internal sealed class MainForm : Form
{
    private readonly ComboBox microphone = NewCombo();
    private readonly ComboBox destination = NewCombo();
    private readonly ComboBox virtualInput = NewCombo();
    private readonly ComboBox headphones = NewCombo();
    private readonly CheckBox micToggle = new() { Text = "Microphone enabled", Checked = true, AutoSize = true };
    private readonly CheckBox monitorToggle = new() { Text = "Monitor mix", AutoSize = true };
    private readonly ListBox sounds = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Button start = NewButton("▶  Start", true);
    private readonly Button stop = NewButton("■  Stop");
    private readonly Button play = NewButton("▶  Play");
    private readonly Button refresh = NewButton("Refresh devices");
    private readonly Label status = new() { Text = "Ready", AutoSize = true, ForeColor = Color.FromArgb(35, 110, 90), Padding = new Padding(0, 8, 0, 0) };
    private readonly Label mode = new() { AutoSize = true, MaximumSize = new Size(690, 0), ForeColor = Color.DimGray };
    private readonly string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicDJ");
    private readonly DefaultInputSession defaults;
    private AudioEngine? engine;
    private bool closing;

    public MainForm()
    {
        Text = "MicDJ";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        ClientSize = new Size(760, 840);
        MinimumSize = new Size(710, 820);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        defaults = new DefaultInputSession(new WindowsDefaultInputs(), Path.Combine(dataDirectory, "input-session.json"));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 9 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.Controls.Add(new Label { Text = "MicDJ", Font = new Font("Segoe UI", 26, FontStyle.Bold), AutoSize = true }, 0, 0);
        var deviceGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDeviceRow(deviceGrid, 0, "Physical microphone", microphone);
        AddDeviceRow(deviceGrid, 1, "Mix output", destination);
        AddDeviceRow(deviceGrid, 2, "Virtual input", virtualInput);
        AddDeviceRow(deviceGrid, 3, "Headphones / monitor", headphones);
        root.Controls.Add(deviceGrid, 0, 1);
        root.Controls.Add(mode, 0, 2);
        var toggles = new FlowLayoutPanel { Dock = DockStyle.Fill };
        micToggle.Margin = new Padding(0, 5, 30, 0);
        monitorToggle.Margin = new Padding(0, 5, 0, 0);
        toggles.Controls.AddRange([micToggle, monitorToggle]);
        root.Controls.Add(toggles, 0, 3);
        root.Controls.Add(new Label { Text = "SOUND LIBRARY · WAV", AutoSize = true, ForeColor = Color.DimGray }, 0, 4);
        sounds.BackColor = Color.White;
        sounds.ItemHeight = 28;
        root.Controls.Add(sounds, 0, 5);
        var libraryActions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var add = NewButton("+  Add WAV");
        var remove = NewButton("Remove from list");
        libraryActions.Controls.AddRange([add, play, remove]);
        root.Controls.Add(libraryActions, 0, 6);
        var transport = new FlowLayoutPanel { Dock = DockStyle.Fill };
        transport.Controls.AddRange([start, stop, refresh]);
        root.Controls.Add(transport, 0, 7);
        root.Controls.Add(status, 0, 8);
        Controls.Add(root);
        start.Click += (_, _) => StartMix();
        stop.Click += (_, _) => StopMix();
        refresh.Click += (_, _) => Guard(RefreshDevices);
        add.Click += (_, _) => AddSounds();
        remove.Click += (_, _) => Guard(RemoveSound);
        play.Click += (_, _) => Guard(PlaySound);
        sounds.DoubleClick += (_, _) => Guard(PlaySound);
        micToggle.CheckedChanged += (_, _) => { if (engine != null) engine.MicrophoneEnabled = micToggle.Checked; };
        monitorToggle.CheckedChanged += (_, _) => { if (engine != null) engine.MonitorEnabled = monitorToggle.Checked; };
        destination.SelectedIndexChanged += (_, _) => UpdateMode();
        Shown += (_, _) =>
        {
            Guard(() => defaults.Restore());
            Guard(RefreshDevices);
            Guard(LoadLibrary);
            UpdateState();
        };
        FormClosing += (_, e) =>
        {
            closing = true;
            if (!StopMix()) { e.Cancel = true; closing = false; }
        };
        UpdateState();
    }

    private static ComboBox NewCombo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 600 };
    private static Button NewButton(string text, bool accent = false) => new()
    {
        Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(115, 36), FlatStyle = FlatStyle.Flat,
        BackColor = accent ? Color.FromArgb(40, 95, 225) : Color.White,
        ForeColor = accent ? Color.White : Color.FromArgb(30, 40, 55), Margin = new Padding(0, 5, 10, 0), Cursor = Cursors.Hand
    };
    private static void AddDeviceRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        control.Margin = new Padding(0, 3, 0, 10);
        panel.Controls.Add(control, 1, row);
    }
    private void RefreshDevices()
    {
        var inputs = AudioEngine.ListDevices(DataFlow.Capture);
        var outputs = AudioEngine.ListDevices(DataFlow.Render);
        static void Fill(ComboBox combo, IEnumerable<DeviceChoice> items, string? preferred)
        {
            string? old = (combo.SelectedItem as DeviceChoice)?.Id;
            combo.Items.Clear();
            combo.Items.AddRange(items.Cast<object>().ToArray());
            var selected = combo.Items.Cast<DeviceChoice>().FirstOrDefault(x => x.Id == old)
                ?? combo.Items.Cast<DeviceChoice>().FirstOrDefault(x => x.Id == preferred);
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        Fill(microphone, inputs, AudioEngine.DefaultDevice(DataFlow.Capture));
        Fill(headphones, outputs, AudioEngine.DefaultDevice(DataFlow.Render));
        var cableOut = outputs.FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
        var cableIn = inputs.FirstOrDefault(x => x.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));
        Fill(destination, new[] { new DeviceChoice("", "Monitor only — no virtual microphone") }.Concat(outputs), cableOut?.Id ?? "");
        Fill(virtualInput, inputs, cableIn?.Id);
        UpdateMode();
    }
    private bool PreviewOnly => (destination.SelectedItem as DeviceChoice)?.Id is null or "";
    private void UpdateMode()
    {
        virtualInput.Enabled = engine == null && !PreviewOnly;
        mode.Text = PreviewOnly
            ? "Test mode: enable Monitor mix to hear the mix. Windows input settings stay unchanged."
            : "Select a cable pair, e.g. CABLE Input → CABLE Output. Start switches the Windows input; Stop restores it.";
    }
    private void UpdateState()
    {
        bool running = engine != null;
        start.Enabled = !running && !defaults.HasPendingRestore;
        stop.Enabled = running || defaults.HasPendingRestore;
        play.Enabled = running;
        microphone.Enabled = destination.Enabled = headphones.Enabled = refresh.Enabled = !running;
        UpdateMode();
    }
    private static string SelectedId(ComboBox combo) => (combo.SelectedItem as DeviceChoice)?.Id
        ?? throw new InvalidOperationException("Select the required audio devices. Connect them and click Refresh devices.");

    private void StartMix()
    {
        try
        {
            string mic = SelectedId(microphone);
            string phones = SelectedId(headphones);
            string? output = PreviewOnly ? null : SelectedId(destination);
            string? input = PreviewOnly ? null : SelectedId(virtualInput);
            if (input == mic) throw new InvalidOperationException("The physical microphone and virtual input must be different devices.");
            if (output == phones) throw new InvalidOperationException("Monitoring must use a different device from the mix output.");
            defaults.Restore();
            engine = new AudioEngine();
            var currentEngine = engine;
            engine.Failed += message =>
            {
                if (!IsDisposed && IsHandleCreated && !closing)
                    BeginInvoke((Action)(() => { if (engine == currentEngine) { StopMix(); ShowError(message); } }));
            };
            engine.Start(mic, output, phones, micToggle.Checked, monitorToggle.Checked);
            if (input != null) defaults.Begin(input);
            status.Text = PreviewOnly ? "● Running · test mode" : "● Running · mix as Windows input";
        }
        catch (Exception ex) { StopMix(); ShowError(ex.Message); }
        UpdateState();
    }

    private bool StopMix()
    {
        List<string> errors = [];
        // Restore first, even if subsequently disposing an unplugged endpoint fails.
        try { defaults.Restore(); } catch (Exception ex) { errors.Add(ex.Message); }
        try { engine?.Dispose(); } catch (Exception ex) { errors.Add(ex.Message); }
        engine = null;
        status.Text = errors.Count == 0 ? "Stopped · previous input preserved" : "Click Stop again to retry";
        UpdateState();
        if (errors.Count > 0) ShowError(string.Join(Environment.NewLine, errors));
        return !defaults.HasPendingRestore;
    }

    private string LibraryDirectory => Path.Combine(dataDirectory, "Sounds");
    private sealed record SoundItem(string Path, bool BuiltIn = false)
    {
        public override string ToString() => BuiltIn ? "♫  Welcome · sample sound" : System.IO.Path.GetFileNameWithoutExtension(Path);
    }
    private void LoadLibrary()
    {
        Directory.CreateDirectory(LibraryDirectory);
        sounds.Items.Clear();
        sounds.Items.Add(new SoundItem(Path.Combine(AppContext.BaseDirectory, "Assets", "chime.wav"), true));
        foreach (string path in Directory.EnumerateFiles(LibraryDirectory, "*.wav")) sounds.Items.Add(new SoundItem(path));
        sounds.SelectedIndex = 0;
    }
    private void AddSounds()
    {
        using var dialog = new OpenFileDialog { Filter = "WAV files (*.wav)|*.wav", Multiselect = true, Title = "Add sounds (up to 2 minutes)" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (string source in dialog.FileNames)
            Guard(() =>
            {
                AudioEngine.LoadSound(source);
                Directory.CreateDirectory(LibraryDirectory);
                string target = Path.Combine(LibraryDirectory, Path.GetFileName(source));
                if (File.Exists(target)) target = Path.Combine(LibraryDirectory, $"{Path.GetFileNameWithoutExtension(source)}-{Guid.NewGuid().ToString("N")[..6]}.wav");
                File.Copy(source, target);
                sounds.Items.Add(new SoundItem(target));
                sounds.SelectedIndex = sounds.Items.Count - 1;
            });
    }
    private void RemoveSound()
    {
        if (sounds.SelectedItem is not SoundItem item) return;
        if (item.BuiltIn) throw new InvalidOperationException("The sample sound is a permanent part of the library.");
        File.Delete(item.Path);
        sounds.Items.Remove(item);
    }
    private void PlaySound()
    {
        if (engine == null || sounds.SelectedItem is not SoundItem sound) return;
        engine.Play(AudioEngine.LoadSound(sound.Path));
    }
    private void Guard(Action action)
    {
        try { action(); } catch (Exception ex) { ShowError(ex.Message); }
    }
    private void ShowError(string message) => MessageBox.Show(this, message, "MicDJ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
