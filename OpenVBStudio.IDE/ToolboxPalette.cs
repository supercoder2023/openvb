using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenVBStudio.IDE;

internal sealed class ToolboxPalette : UserControl
{
    private static readonly (string Name, string Icon)[] Items =
    [
        ("Pointer", "pointer"),
        ("Label", "label"),
        ("TextBox", "textbox"),
        ("CommandButton", "button"),
        ("CheckBox", "checkbox"),
        ("OptionButton", "option"),
        ("ComboBox", "combo"),
        ("ListBox", "list"),
        ("Frame", "frame")
    ];

    private readonly ToolTip _toolTip = new();
    private readonly List<ToolboxItemButton> _buttons = [];
    private string? _selectedKind = "Pointer";

    public ToolboxPalette()
    {
        BackColor = SystemColors.Control;

        var category = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 24
        };
        category.Items.Add("General");
        category.SelectedIndex = 0;
        category.Enabled = false;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(3),
            BackColor = SystemColors.Control
        };
        for (var column = 0; column < grid.ColumnCount; column++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        for (var row = 0; row < grid.RowCount; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        for (var index = 0; index < Items.Length; index++)
        {
            var item = Items[index];
            var button = new ToolboxItemButton(item.Icon)
            {
                AccessibleName = item.Name,
                AccessibleDescription = $"Select or drag {item.Name}",
                Margin = new Padding(2),
                Size = new Size(28, 28),
                Tag = item.Name
            };
            _toolTip.SetToolTip(button, item.Name);
            button.Click += (_, _) => Select(item.Name);
            button.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    Select(item.Name);
            };
            button.MouseMove += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && item.Name != "Pointer")
                {
                    Select(item.Name);
                    button.DoDragDrop(item.Name, DragDropEffects.Copy);
                }
            };
            _buttons.Add(button);
            grid.Controls.Add(button, index % 3, index / 3);
        }

        Controls.Add(grid);
        Controls.Add(category);
        UpdateSelection();
    }

    public string? SelectedKind => _selectedKind;

    public void SelectPointer() => Select("Pointer");

    private void Select(string kind)
    {
        _selectedKind = kind;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var button in _buttons)
            button.SetSelected(Equals(button.Tag, _selectedKind));
    }
}

internal sealed class ToolboxItemButton(string icon) : Button
{
    private bool _isSelected;

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(_isSelected ? SystemColors.Highlight : SystemColors.Control);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var foreground = _isSelected ? SystemColors.HighlightText : SystemColors.ControlText;
        using var line = new Pen(foreground, 1.5f);
        using var fill = new SolidBrush(foreground);
        var bounds = new Rectangle(5, 5, Width - 11, Height - 11);

        switch (icon)
        {
            case "pointer":
                Point[] pointer = [new(6, 3), new(6, 20), new(10, 16), new(13, 22), new(16, 20), new(13, 14), new(19, 14)];
                e.Graphics.FillPolygon(fill, pointer);
                break;
            case "label":
                using (var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(e.Graphics, "A", font, ClientRectangle, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                break;
            case "textbox":
                e.Graphics.DrawRectangle(line, bounds);
                TextRenderer.DrawText(e.Graphics, "ab", Font, bounds, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                break;
            case "button":
                e.Graphics.DrawRectangle(line, bounds);
                break;
            case "checkbox":
                e.Graphics.DrawRectangle(line, 5, 7, 11, 11);
                e.Graphics.DrawLines(line, [new Point(7, 12), new Point(10, 15), new Point(15, 8)]);
                break;
            case "option":
                e.Graphics.DrawEllipse(line, 5, 6, 13, 13);
                e.Graphics.FillEllipse(fill, 9, 10, 5, 5);
                break;
            case "combo":
                e.Graphics.DrawRectangle(line, 4, 7, 18, 12);
                e.Graphics.DrawLine(line, 17, 7, 17, 19);
                e.Graphics.FillPolygon(fill, [new Point(19, 12), new Point(22, 12), new Point(20, 15)]);
                break;
            case "list":
                e.Graphics.DrawRectangle(line, 5, 5, 17, 18);
                for (var row = 0; row < 3; row++)
                    e.Graphics.DrawLine(line, 8, 10 + row * 5, 18, 10 + row * 5);
                break;
            case "frame":
                e.Graphics.DrawRectangle(line, 4, 7, 19, 15);
                using (var background = new SolidBrush(_isSelected ? SystemColors.Highlight : SystemColors.Control))
                    e.Graphics.FillRectangle(background, 7, 4, 10, 6);
                TextRenderer.DrawText(e.Graphics, "F", Font, new Rectangle(7, 2, 10, 10), foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                break;
        }
    }
}
