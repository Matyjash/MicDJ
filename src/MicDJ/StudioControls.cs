using System.Drawing.Drawing2D;

namespace MicDJ;

internal static class StudioTheme
{
    public static readonly Color Background = Color.FromArgb(16, 16, 18);
    public static readonly Color Panel = Color.FromArgb(28, 28, 32);
    public static readonly Color Field = Color.FromArgb(36, 36, 41);
    public static readonly Color Text = Color.FromArgb(255, 245, 230);
    public static readonly Color Muted = Color.FromArgb(174, 171, 169);
    public static readonly Color Border = Color.FromArgb(100, 100, 104);
    public static readonly Color Red = Color.FromArgb(255, 23, 50);
    public static readonly Color Selection = Color.FromArgb(58, 23, 31);
}

internal sealed class StudioButton : Button
{
    [System.ComponentModel.DefaultValue(false)]
    public bool Accent { get; init; }
    [System.ComponentModel.DefaultValue(false)]
    public bool KeyboardIcon { get; init; }
    private bool hovered;
    public StudioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        Margin = Padding.Empty;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Color fill = Accent && Enabled ? StudioTheme.Red : StudioTheme.Field;
        if (hovered && Enabled) fill = ControlPaint.Light(fill, .12f);
        e.Graphics.Clear(fill);
        using var border = new Pen(Focused ? StudioTheme.Text : Accent && Enabled ? StudioTheme.Red : StudioTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        var textBounds = ClientRectangle;
        if (KeyboardIcon)
        {
            float scale = DeviceDpi / 96f;
            float x = 12 * scale, y = (Height - 18 * scale) / 2;
            using var pen = new Pen(StudioTheme.Text, scale);
            using var key = new SolidBrush(StudioTheme.Text);
            e.Graphics.DrawRectangle(pen, x, y, 28 * scale, 18 * scale);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 5; column++)
                    e.Graphics.FillRectangle(key, x + (4 + column * 4) * scale, y + (4 + row * 4) * scale, 2 * scale, 2 * scale);
            e.Graphics.FillRectangle(key, x + 7 * scale, y + 13 * scale, 14 * scale, 2 * scale);
            textBounds.X += (int)(46 * scale);
            textBounds.Width -= (int)(46 * scale);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? StudioTheme.Text : StudioTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}

internal sealed class StudioToggle : CheckBox
{
    public StudioToggle()
    {
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        BackColor = StudioTheme.Background;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        // Repaint the full surface so hover and focus cannot leave native checkbox pixels behind.
        e.Graphics.Clear(BackColor);
        float scale = DeviceDpi / 96f;
        int width = (int)(36 * scale), height = (int)(20 * scale), inset = (int)(4 * scale);
        var track = new Rectangle(0, (Height - height) / 2, width, height);
        using var fill = new SolidBrush(Checked ? StudioTheme.Red : StudioTheme.Border);
        e.Graphics.FillRectangle(fill, track);
        using var thumb = new SolidBrush(StudioTheme.Text);
        int size = height - 2 * inset;
        e.Graphics.FillRectangle(thumb, track.Left + (Checked ? width - inset - size : inset), track.Top + inset, size, size);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(width + (int)(8 * scale), 0, Width - width - (int)(8 * scale), Height), StudioTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}

internal sealed class StudioCombo : ComboBox
{
    public StudioCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        Dock = DockStyle.Fill;
        DropDownWidth = 600;
        FlatStyle = FlatStyle.Flat;
        BackColor = StudioTheme.Field;
        ForeColor = StudioTheme.Text;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 28;
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x000F && m.Msg != 0x0317 && m.Msg != 0x0318) return;
        // Replace the complete native face, including its inset white border.
        using var graphics = m.Msg == 0x000F ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam);
        int arrowWidth = (int)(20 * DeviceDpi / 96f);
        using var fill = new SolidBrush(StudioTheme.Field);
        graphics.FillRectangle(fill, ClientRectangle);
        int padding = (int)(6 * DeviceDpi / 96f);
        var textBounds = new Rectangle(padding, 1, Math.Max(0, Width - arrowWidth - padding), Height - 2);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, Enabled ? StudioTheme.Text : StudioTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        using var border = new Pen(StudioTheme.Border);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var arrow = new Pen(Enabled ? StudioTheme.Text : StudioTheme.Muted, 1.5f * DeviceDpi / 96f);
        int x = Width - arrowWidth / 2, y = Height / 2;
        graphics.DrawLines(arrow, [new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2)]);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(textBounds, -2, -2), StudioTheme.Muted, StudioTheme.Field);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? StudioTheme.Selection : StudioTheme.Field);
        e.Graphics.FillRectangle(fill, e.Bounds);
        string text = (e.Index >= 0 ? GetItemText(Items[e.Index]) : Text) ?? string.Empty;
        TextRenderer.DrawText(e.Graphics, text, Font, Rectangle.Inflate(e.Bounds, -5, 0), Enabled ? StudioTheme.Text : StudioTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}

internal sealed class StudioLibrary : ListBox
{
    public StudioLibrary()
    {
        Dock = DockStyle.Fill;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = StudioTheme.Background;
        ForeColor = StudioTheme.Text;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 52;
        AccessibleName = "Sound library";
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = (int)(52 * DeviceDpi / 96f);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        float scale = DeviceDpi / 96f;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? StudioTheme.Selection : BackColor);
        e.Graphics.FillRectangle(fill, e.Bounds);
        if (selected)
        {
            using var accent = new SolidBrush(StudioTheme.Red);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, 4 * scale, e.Bounds.Height);
        }
        using var line = new Pen(StudioTheme.Border);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        int x = e.Bounds.Left + (int)(19 * scale), y = e.Bounds.Top + (int)(15 * scale);
        using var icon = new Pen(StudioTheme.Text, scale);
        e.Graphics.DrawLines(icon, [new PointF(x, y), new PointF(x + 9 * scale, y), new PointF(x + 14 * scale, y + 5 * scale), new PointF(x + 14 * scale, y + 21 * scale), new PointF(x, y + 21 * scale), new PointF(x, y)]);
        e.Graphics.DrawLines(icon, [new PointF(x + 9 * scale, y), new PointF(x + 9 * scale, y + 5 * scale), new PointF(x + 14 * scale, y + 5 * scale)]);
        var textBounds = new Rectangle(e.Bounds.Left + (int)(46 * scale), e.Bounds.Top, e.Bounds.Width - (int)(54 * scale), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, textBounds, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}

internal sealed class BrandHeader : Control
{
    private readonly Image brand = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "brand.png"));
    public BrandHeader()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleName = "MicDJ";
        BackColor = Color.Black;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DeviceDpi / 96f;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        // Both source regions come from the approved logo, preserving its custom lettering.
        e.Graphics.DrawImage(brand, new RectangleF(24 * scale, 14 * scale, 75 * scale, 59 * scale), new RectangleF(355, 92, 825, 618), GraphicsUnit.Pixel);
        e.Graphics.DrawImage(brand, new RectangleF(110 * scale, 30 * scale, 148 * scale, 31 * scale), new RectangleF(280, 733, 984, 212), GraphicsUnit.Pixel);
        using var border = new Pen(StudioTheme.Border);
        e.Graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) brand.Dispose();
        base.Dispose(disposing);
    }
}
