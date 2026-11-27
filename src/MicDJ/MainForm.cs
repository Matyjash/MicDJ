using MicDJ.Audio;
using MicDJ.Core;
using NAudio.CoreAudioApi;

namespace MicDJ;

internal sealed partial class MainForm : Form
{
    private readonly ComboBox microphone = NewCombo();
    private readonly ComboBox destination = NewCombo();
    private readonly ComboBox virtualInput = NewCombo();
    private readonly ComboBox headphones = NewCombo();
    private readonly CheckBox micToggle = new StudioToggle { Text = "Microphone enabled", Checked = true };
    private readonly CheckBox monitorToggle = new StudioToggle { Text = "Monitor mix" };
    private readonly StudioLibrary sounds = new();
    private readonly Button start = NewButton("▶  Start", true);
    private readonly Button stop = NewButton("■  Stop");
    private readonly Button play = NewButton("▶  Play");
    private readonly Button refresh = NewButton("Refresh devices");
    private readonly Label status = new() { Text = "Ready", AutoSize = true, ForeColor = Color.FromArgb(35, 110, 90), Padding = new Padding(0, 8, 0, 0) };
    private readonly string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicDJ");
    private readonly DefaultInputSession defaults;
    private AudioEngine? engine;
    private bool closing;
    private readonly System.Windows.Forms.Timer playbackTimer = new() { Interval = 80 };
    private SoundItem? playingSound;
    private DeviceSelections deviceSelections = new();
    private string DeviceSelectionsPath => Path.Combine(dataDirectory, "devices.json");
    private KeyMappings keyMappings = new();
    private readonly GlobalHotKeys hotKeys = new();
    private string KeyMappingsPath => Path.Combine(dataDirectory, "key-mappings.json");

    public MainForm()
    {
        Text = "MicDJ";
        Font = new Font("Bahnschrift", 10);
        BackColor = StudioTheme.Background;
        ForeColor = StudioTheme.Text;
        ClientSize = new Size(760, 840);
        MinimumSize = new Size(710, 820);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        defaults = new DefaultInputSession(new WindowsDefaultInputs(), Path.Combine(dataDirectory, "input-session.json"));

        var (add, remove) = BuildStudioLayout();
        sounds.ShortcutText = item => item is SoundItem sound && keyMappings.Bindings.TryGetValue(sound.Id, out var shortcut)
            ? KeyMappings.Format(shortcut) : null;
        playbackTimer.Tick += (_, _) => UpdatePlayback();
        FormClosed += (_, _) => playbackTimer.Dispose();
        Disposed += (_, _) => hotKeys.Dispose();
        hotKeys.Pressed += id =>
        {
            if (engine == null || !Enabled || closing || ContainsOpenDropdown()) return;
            var sound = sounds.Items.Cast<SoundItem>().FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Id, id));
            if (sound != null) Guard(() => PlaySound(sound));
        };
        start.Click += (_, _) => StartMix();
        stop.Click += (_, _) => StopMix();
        refresh.Click += (_, _) => Guard(RefreshDevices);
        add.Click += (_, _) => AddSounds();
        remove.Click += (_, _) => Guard(RemoveSound);
        play.Click += (_, _) => Guard(PlaySound);
        sounds.DoubleClick += (_, _) => Guard(PlaySound);
        sounds.SelectedIndexChanged += (_, _) => UpdatePlayButton();
        micToggle.CheckedChanged += (_, _) => { if (engine != null) engine.MicrophoneEnabled = micToggle.Checked; };
        monitorToggle.CheckedChanged += (_, _) => { if (engine != null) engine.MonitorEnabled = monitorToggle.Checked; };
        destination.SelectedIndexChanged += (_, _) => UpdateMode();
        void Remember(ComboBox combo, Action<string> assign)
        {
            combo.SelectionChangeCommitted += (_, _) => Guard(() =>
            {
                assign(SelectedId(combo));
                deviceSelections.Save(DeviceSelectionsPath);
            });
        }
        Remember(microphone, id => deviceSelections.Microphone = id);
        Remember(destination, id => deviceSelections.Destination = id);
        Remember(virtualInput, id => deviceSelections.VirtualInput = id);
        Remember(headphones, id => deviceSelections.Headphones = id);
        Shown += (_, _) =>
        {
            Guard(() => defaults.Restore());
            Guard(() => deviceSelections = DeviceSelections.Load(DeviceSelectionsPath));
            Guard(RefreshDevices);
            Guard(LoadLibrary);
            Guard(LoadKeyMappings);
            UpdateState();
        };
        FormClosing += (_, e) =>
        {
            closing = true;
            if (!StopMix()) { e.Cancel = true; closing = false; }
        };
        UpdateState();
    }

    private static ComboBox NewCombo() => new StudioCombo();
    private static Button NewButton(string text, bool accent = false) => new StudioButton { Text = text, Accent = accent };
    private void RefreshDevices()
    {
        var inputs = AudioEngine.ListDevices(DataFlow.Capture);
        var outputs = AudioEngine.ListDevices(DataFlow.Render);
        static void Fill(ComboBox combo, IEnumerable<DeviceChoice> items, string? saved, string? preferred)
        {
            string? old = (combo.SelectedItem as DeviceChoice)?.Id;
            combo.Items.Clear();
            combo.Items.AddRange(items.Cast<object>().ToArray());
            var selected = combo.Items.Cast<DeviceChoice>().FirstOrDefault(x => x.Id == saved)
                ?? combo.Items.Cast<DeviceChoice>().FirstOrDefault(x => x.Id == old)
                ?? combo.Items.Cast<DeviceChoice>().FirstOrDefault(x => x.Id == preferred);
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        Fill(microphone, inputs, deviceSelections.Microphone, AudioEngine.DefaultDevice(DataFlow.Capture));
        Fill(headphones, outputs, deviceSelections.Headphones, AudioEngine.DefaultDevice(DataFlow.Render));
        var cableOut = outputs.FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
        var cableIn = inputs.FirstOrDefault(x => x.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));
        Fill(destination, new[] { new DeviceChoice("", "Monitor only — no virtual microphone") }.Concat(outputs), deviceSelections.Destination, cableOut?.Id ?? "");
        Fill(virtualInput, inputs, deviceSelections.VirtualInput, cableIn?.Id);
        UpdateMode();
    }
    private bool PreviewOnly => (destination.SelectedItem as DeviceChoice)?.Id is null or "";
    private void UpdateMode()
    {
        virtualInput.Enabled = engine == null && !PreviewOnly;

    }
    private void UpdateState()
    {
        bool running = engine != null;
        start.Enabled = !running && !defaults.HasPendingRestore;
        stop.Enabled = running || defaults.HasPendingRestore;
        UpdatePlayButton();
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
            RegisterHotKeys();
        }
        catch (Exception ex) { StopMix(); ShowError(ex.Message); }
        UpdateState();
    }

    private bool StopMix()
    {
        hotKeys.Clear();
        List<string> errors = [];
        // Restore first, even if subsequently disposing an unplugged endpoint fails.
        try { defaults.Restore(); } catch (Exception ex) { errors.Add(ex.Message); }
        try { engine?.Dispose(); } catch (Exception ex) { errors.Add(ex.Message); }
        engine = null;
        UpdatePlayback();
        status.Text = errors.Count == 0 ? "Stopped · previous input preserved" : "Click Stop again to retry";
        UpdateState();
        if (errors.Count > 0) ShowError(string.Join(Environment.NewLine, errors));
        return !defaults.HasPendingRestore;
    }

    private string LibraryDirectory => Path.Combine(dataDirectory, "Sounds");
    private sealed record SoundItem(string Path, bool BuiltIn = false)
    {
        public string Id => BuiltIn ? "builtin:welcome" : "sound:" + System.IO.Path.GetFileName(Path);
        public override string ToString() => BuiltIn ? "Welcome · sample sound" : System.IO.Path.GetFileNameWithoutExtension(Path);
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
        if (playingSound == item)
        {
            engine?.StopSound();
            UpdatePlayback();
        }
        sounds.Items.Remove(item);
        keyMappings.Remove(item.Id);
        RegisterHotKeys();
        keyMappings.Save(KeyMappingsPath);
    }
    private void PlaySound()
    {
        if (engine == null || sounds.SelectedItem is not SoundItem sound) return;
        if (playingSound == sound && engine.IsPlaying)
        {
            engine.StopSound();
            UpdatePlayback();
            return;
        }
        PlaySound(sound);
    }

    private void PlaySound(SoundItem sound)
    {
        if (engine == null) return;
        engine.Play(AudioEngine.LoadSound(sound.Path));
        playingSound = sound;
        playbackTimer.Start();
        UpdatePlayback();
    }

    private void UpdatePlayback()
    {
        if (engine?.IsPlaying != true)
        {
            playingSound = null;
            playbackTimer.Stop();
        }
        sounds.PlayingItem = playingSound;
        sounds.AnimationFrame = (sounds.AnimationFrame + 1) % 12;
        sounds.Invalidate();
        UpdatePlayButton();
    }

    private void UpdatePlayButton()
    {
        play.Enabled = engine != null && sounds.SelectedItem is SoundItem;
        play.Text = engine?.IsPlaying == true && sounds.SelectedItem is SoundItem selected && playingSound == selected
            ? "■  Stop sound" : "▶  Play";
    }

    private void LoadKeyMappings()
    {
        var loaded = KeyMappings.Load(KeyMappingsPath);
        var available = sounds.Items.Cast<SoundItem>().Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string id in loaded.Bindings.Keys.Where(id => !available.Contains(id)).ToArray()) loaded.Remove(id);
        keyMappings = loaded;
        sounds.Invalidate();
    }

    private void OpenKeyMappings()
    {
        hotKeys.Clear();
        try
        {
            using var dialog = new KeyMappingsForm(sounds.Items.Cast<SoundItem>().Select(item => new SoundChoice(item.Id, item.ToString())).ToArray(), keyMappings, KeyMappingsPath);
            if (dialog.ShowDialog(this) == DialogResult.OK) Guard(LoadKeyMappings);
        }
        finally { RegisterHotKeys(); }
    }

    private void RegisterHotKeys()
    {
        if (engine == null) return;
        var failures = hotKeys.Register(keyMappings);
        if (failures.Count > 0)
            ShowError("Some global shortcuts could not be activated. Choose different combinations in Key mappings.\n\n" + string.Join(Environment.NewLine, failures));
    }

    private bool ContainsOpenDropdown() => microphone.DroppedDown || destination.DroppedDown || virtualInput.DroppedDown || headphones.DroppedDown;
    private void Guard(Action action)
    {
        try { action(); } catch (Exception ex) { ShowError(ex.Message); }
    }
    private void ShowError(string message) => MessageBox.Show(this, message, "MicDJ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
