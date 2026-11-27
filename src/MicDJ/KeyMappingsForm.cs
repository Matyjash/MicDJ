using MicDJ.Core;
using System.Runtime.InteropServices;

namespace MicDJ;

internal sealed class KeyMappingsForm : Form
{
    private readonly SoundChoice[] library;
    private readonly KeyMappings draft;
    private readonly string savePath;
    private readonly TextBox search = new() { PlaceholderText = "Search sounds by name…", Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly StudioCombo sound = new();
    private readonly ShortcutCapture capture = new() { Dock = DockStyle.Fill };
    private readonly Label feedback = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly StudioButton assign = new() { Text = "Assign", Accent = true, Dock = DockStyle.Fill, Enabled = false };
    private readonly DataGridView rows = new();
    private readonly Label listTitle = new() { Text = "Existing mappings", Dock = DockStyle.Fill, Font = new Font("Bahnschrift", 18, FontStyle.Bold) };
    private readonly Label emptyState = new() { Dock = DockStyle.Fill, BackColor = StudioTheme.Background, ForeColor = StudioTheme.Muted, TextAlign = ContentAlignment.MiddleCenter, Text = "No shortcuts assigned yet\nChoose a sound above to create your first mapping." };

    public KeyMappingsForm(SoundChoice[] library, KeyMappings mappings, string savePath)
    {
        this.library = library;
        draft = mappings.Clone();
        this.savePath = savePath;
        Text = "MicDJ — Key mappings";
        Font = new Font("Bahnschrift", 10);
        BackColor = StudioTheme.Background;
        ForeColor = StudioTheme.Text;
        ClientSize = new Size(760, 840);
        MinimumSize = new Size(710, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "MicDJ.ico"));
        search.BackColor = StudioTheme.Field;
        search.ForeColor = StudioTheme.Text;
        search.AccessibleName = "Search sounds by name";
        sound.AccessibleName = "Sound to assign";
        BuildLayout();
        search.TextChanged += (_, _) => FilterSounds();
        sound.SelectedIndexChanged += (_, _) => { capture.Reset(); UpdateFeedback(); };
        capture.ShortcutChanged += (_, _) => UpdateFeedback();
        assign.Click += (_, _) => Assign();
        FilterSounds();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Black, ColumnCount = 2, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(new BrandHeader { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        header.Controls.Add(new Label { Text = "Key mappings", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 20, 0), Font = new Font("Bahnschrift", 18, FontStyle.Bold) }, 1, 0);
        header.Paint += (_, e) => { using var pen = new Pen(StudioTheme.Border); e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20), ColumnCount = 1, RowCount = 9 };
        foreach (int height in new[] { 36, 28, 44, 26, 44, 128, 48, 40 }) body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(new Label { Text = "Assign a shortcut", Dock = DockStyle.Fill, Margin = Padding.Empty, Font = new Font("Bahnschrift", 18, FontStyle.Bold) }, 0, 0);
        body.Controls.Add(new Label { Text = "Select a sound, capture a shortcut, then assign it.", Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = StudioTheme.Muted }, 0, 1);
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10), ColumnCount = 2, RowCount = 1 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchRow.Controls.Add(new Label { Text = "Search sounds", Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var searchFrame = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = StudioTheme.Field, Padding = new Padding(10, 8, 10, 5) };
        searchFrame.Paint += (_, e) => { using var pen = new Pen(search.ContainsFocus ? StudioTheme.Red : StudioTheme.Border); e.Graphics.DrawRectangle(pen, 0, 0, searchFrame.Width - 1, searchFrame.Height - 1); };
        search.Enter += (_, _) => searchFrame.Invalidate();
        search.Leave += (_, _) => searchFrame.Invalidate();
        search.BorderStyle = BorderStyle.None;
        searchFrame.Controls.Add(search);
        searchRow.Controls.Add(searchFrame, 1, 0);
        body.Controls.Add(searchRow, 0, 2);
        body.Controls.Add(new Label { Text = "Sound", Dock = DockStyle.Fill, Margin = Padding.Empty, Font = new Font("Bahnschrift", 10, FontStyle.Bold) }, 0, 3);
        sound.Margin = new Padding(0, 0, 0, 10);
        body.Controls.Add(sound, 0, 4);

        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = StudioTheme.Panel, Padding = new Padding(16), ColumnCount = 2, RowCount = 2 };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var hint = new Label { Text = "Shortcut", Dock = DockStyle.Fill, Margin = Padding.Empty, Font = new Font("Bahnschrift", 10, FontStyle.Bold) };
        editor.Controls.Add(hint, 0, 0);
        editor.SetColumnSpan(hint, 2);
        capture.Margin = Padding.Empty;
        assign.Margin = new Padding(12, 0, 0, 0);
        editor.Controls.Add(capture, 0, 1);
        editor.Controls.Add(assign, 1, 1);
        body.Controls.Add(editor, 0, 5);
        feedback.Margin = Padding.Empty;
        body.Controls.Add(feedback, 0, 6);
        listTitle.Margin = Padding.Empty;
        body.Controls.Add(listTitle, 0, 7);
        ConfigureRows();
        var listFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = StudioTheme.Border, Margin = Padding.Empty };
        listFrame.Controls.Add(rows);
        listFrame.Controls.Add(emptyState);
        body.Controls.Add(listFrame, 0, 8);
        root.Controls.Add(body, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 22, 20, 22), ColumnCount = 3, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(new Label { Text = "Changes apply when saved", ForeColor = StudioTheme.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
        var save = new StudioButton { Text = "Save mappings", Accent = true, Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new StudioButton { Text = "Cancel", Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        footer.Controls.Add(cancel, 1, 0);
        footer.Controls.Add(save, 2, 0);
        footer.Paint += (_, e) => { using var pen = new Pen(StudioTheme.Border); e.Graphics.DrawLine(pen, 20, 0, footer.Width - 20, 0); };
        CancelButton = cancel;
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
    }
    private void ConfigureRows()
    {
        rows.Dock = DockStyle.Fill;
        rows.BackgroundColor = StudioTheme.Background;
        rows.BorderStyle = BorderStyle.None;
        rows.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        rows.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        rows.ColumnHeadersHeight = 36;
        rows.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        rows.GridColor = StudioTheme.Border;
        rows.EnableHeadersVisualStyles = false;
        rows.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = StudioTheme.Panel, ForeColor = StudioTheme.Text };
        rows.ColumnHeadersDefaultCellStyle.SelectionBackColor = StudioTheme.Panel;
        rows.ColumnHeadersDefaultCellStyle.SelectionForeColor = StudioTheme.Text;
        rows.DefaultCellStyle = new DataGridViewCellStyle { BackColor = StudioTheme.Background, ForeColor = StudioTheme.Text, SelectionBackColor = StudioTheme.Selection, SelectionForeColor = StudioTheme.Text, Padding = new Padding(12, 0, 12, 0) };
        rows.RowTemplate.Height = 52;
        rows.RowHeadersVisible = false;
        rows.AllowUserToAddRows = rows.AllowUserToDeleteRows = rows.AllowUserToResizeRows = false;
        rows.ReadOnly = true;
        rows.MultiSelect = false;
        rows.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        rows.ScrollBars = ScrollBars.Vertical;
        rows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        rows.AccessibleName = "Existing key mappings";
        rows.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sound", FillWeight = 60, SortMode = DataGridViewColumnSortMode.NotSortable });
        rows.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shortcut", FillWeight = 40, SortMode = DataGridViewColumnSortMode.NotSortable });
        rows.Columns.Add(new DataGridViewButtonColumn { HeaderText = "", Text = "Edit", UseColumnTextForButtonValue = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 64, FlatStyle = FlatStyle.Flat });
        rows.Columns.Add(new DataGridViewButtonColumn { HeaderText = "", Text = "Clear", UseColumnTextForButtonValue = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 64, FlatStyle = FlatStyle.Flat });
        rows.CellPainting += PaintMappingCell;
        rows.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || rows.Rows[e.RowIndex].Tag is not SoundChoice selected) return;
            if (e.ColumnIndex == 2)
            {
                sound.SelectedItem = selected;
                capture.SetShortcut(draft.Bindings[selected.Id]);
                capture.Focus();
            }
            else if (e.ColumnIndex == 3)
            {
                draft.Remove(selected.Id);
                capture.Reset();
                RefreshRows();
                UpdateFeedback();
            }
        };
    }

    private void PaintMappingCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null || e.ColumnIndex < 0) return;
        e.PaintBackground(e.ClipBounds, true);
        bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
        using var border = new Pen(StudioTheme.Border);
        e.Graphics.DrawLine(border, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
        if (e.ColumnIndex == 0)
        {
            e.PaintContent(e.ClipBounds);
            if (selected)
            {
                using var red = new SolidBrush(StudioTheme.Red);
                e.Graphics.FillRectangle(red, e.CellBounds.Left, e.CellBounds.Top, 4 * DeviceDpi / 96f, e.CellBounds.Height - 1);
            }
        }
        else
        {
            int inset = (int)(8 * DeviceDpi / 96f);
            Rectangle box = Rectangle.Inflate(e.CellBounds, -inset, -inset);
            string text = Convert.ToString(e.FormattedValue) ?? string.Empty;
            if (e.ColumnIndex == 1)
                box.Width = Math.Min(box.Width, TextRenderer.MeasureText(text, Font).Width + 18 * DeviceDpi / 96);
            using var fill = new SolidBrush(StudioTheme.Field);
            e.Graphics.FillRectangle(fill, box);
            e.Graphics.DrawRectangle(border, box.X, box.Y, box.Width - 1, box.Height - 1);
            TextRenderer.DrawText(e.Graphics, text, Font, box, StudioTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (rows.Focused && rows.CurrentCell?.RowIndex == e.RowIndex && rows.CurrentCell.ColumnIndex == e.ColumnIndex)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(box, -3, -3));
        }
        e.Handled = true;
    }

    private void FilterSounds()
    {
        string? previous = (sound.SelectedItem as SoundChoice)?.Id;
        sound.BeginUpdate();
        sound.Items.Clear();
        var filtered = library.Where(item => item.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        sound.Items.AddRange(filtered);
        sound.SelectedItem = filtered.FirstOrDefault(item => item.Id == previous);
        if (sound.SelectedIndex < 0 && sound.Items.Count > 0) sound.SelectedIndex = 0;
        sound.EndUpdate();
        sound.Enabled = capture.Enabled = sound.Items.Count > 0;
        RefreshRows();
        UpdateFeedback();
    }

    private void RefreshRows()
    {
        rows.Rows.Clear();
        foreach (var item in library.Where(item => item.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            if (draft.Bindings.TryGetValue(item.Id, out Keys shortcut))
            {
                int index = rows.Rows.Add(item.Name, KeyMappings.Format(shortcut));
                rows.Rows[index].Tag = item;
            }
        listTitle.Text = rows.Rows.Count == 0 ? "Existing mappings" : $"Existing mappings ({rows.Rows.Count})";
        emptyState.Text = search.Text.Trim().Length > 0 ? "No mappings match your search.\nTry another sound name." : "No shortcuts assigned yet\nChoose a sound above to create your first mapping.";
        emptyState.Visible = rows.Rows.Count == 0;
        rows.Visible = rows.Rows.Count > 0;
        if (emptyState.Visible) emptyState.BringToFront();
    }

    private void UpdateFeedback()
    {
        assign.Enabled = false;
        feedback.ForeColor = StudioTheme.Muted;
        if (sound.SelectedItem is not SoundChoice selected) { feedback.Text = "No matching sounds. Add WAV files from the main window."; return; }
        if (capture.Shortcut is not Keys shortcut) { feedback.Text = "Use F1–F24 (except F12), or Ctrl/Alt with a letter or number."; return; }
        if ((shortcut & Keys.KeyCode) == Keys.F12) { feedback.Text = "F12 is reserved by Windows. Choose another shortcut."; feedback.ForeColor = StudioTheme.Red; return; }
        if (!KeyMappings.IsValid(shortcut)) { feedback.Text = "Choose a function key, or Ctrl/Alt with a letter or number."; feedback.ForeColor = StudioTheme.Red; return; }
        string? conflict = draft.FindSound(shortcut);
        if (conflict != null && !StringComparer.OrdinalIgnoreCase.Equals(conflict, selected.Id))
        {
            feedback.Text = $"{KeyMappings.Format(shortcut)} is already assigned to {library.FirstOrDefault(item => item.Id == conflict)?.Name ?? conflict}.";
            feedback.ForeColor = StudioTheme.Red;
            return;
        }
        feedback.Text = $"{KeyMappings.Format(shortcut)} is available for {selected.Name}.";
        assign.Enabled = true;
    }

    private void Assign()
    {
        if (!assign.Enabled || sound.SelectedItem is not SoundChoice selected || capture.Shortcut is not Keys shortcut) return;
        draft.Assign(selected.Id, shortcut);
        RefreshRows();
        capture.Reset();
        feedback.Text = $"Assigned {KeyMappings.Format(shortcut)} to {selected.Name}. Save to apply.";
    }

    private void Save()
    {
        if (capture.Shortcut != null)
        {
            feedback.Text = "Assign the captured shortcut first, or press Esc in the capture field to discard it.";
            return;
        }
        try { draft.Save(savePath); DialogResult = DialogResult.OK; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save key mappings", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        int caption = ColorTranslator.ToWin32(StudioTheme.Panel);
        DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

internal sealed class ShortcutCapture : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Keys? Shortcut { get; private set; }
    public event EventHandler? ShortcutChanged;
    public ShortcutCapture()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleName = "Press shortcut";
        AccessibleRole = AccessibleRole.Text;
    }
    public void Reset() { Shortcut = null; ShortcutChanged?.Invoke(this, EventArgs.Empty); Invalidate(); }
    public void SetShortcut(Keys keys) { Shortcut = keys; ShortcutChanged?.Invoke(this, EventArgs.Empty); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab)) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData == Keys.Escape) { Reset(); return true; }
        Keys key = keyData & Keys.KeyCode;
        if (key is not (Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)) SetShortcut(keyData);
        return true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill = new SolidBrush(StudioTheme.Field);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        using var border = new Pen(Focused ? StudioTheme.Red : StudioTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var font = new Font(Font.FontFamily, Shortcut.HasValue ? 18 : 12, Shortcut.HasValue ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, Shortcut is Keys key ? KeyMappings.Format(key) : Focused ? "Press a shortcut…" : "Click to capture shortcut", font, ClientRectangle, Enabled ? StudioTheme.Text : StudioTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

