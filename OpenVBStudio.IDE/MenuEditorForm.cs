using System.Windows.Forms;
using OpenVBStudio.Core.Projects;

namespace OpenVBStudio.IDE;

internal sealed class MenuEditorForm : Form
{
    private readonly List<MenuItemDocument> _items;
    private readonly TreeView _tree = new();
    private readonly PropertyGrid _properties = new();

    public MenuEditorForm(List<MenuItemDocument> items)
    {
        _items = items;
        Text = "Menu Editor";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 380);
        Size = new Size(760, 470);
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        _tree.Dock = DockStyle.Fill;
        _tree.HideSelection = false;
        _tree.AfterSelect += (_, e) => _properties.SelectedObject = e.Node?.Tag;
        _tree.AfterLabelEdit += (_, e) =>
        {
            if (e.Label is not null && e.Node?.Tag is MenuItemDocument item)
            {
                item.Caption = e.Label;
                _properties.Refresh();
            }
        };
        _properties.Dock = DockStyle.Fill;
        _properties.ToolbarVisible = false;
        _properties.PropertyValueChanged += (_, _) => RefreshTreeLabels();
        root.Controls.Add(_tree, 0, 0);
        root.Controls.Add(_properties, 1, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.Add(CreateButton("Add Top-Level", (_, _) => AddTopLevel()));
        actions.Controls.Add(CreateButton("Add Submenu", (_, _) => AddSubmenu()));
        actions.Controls.Add(CreateButton("Remove", (_, _) => RemoveSelected()));
        actions.Controls.Add(CreateButton("Close", (_, _) => DialogResult = DialogResult.OK));
        root.Controls.Add(actions, 0, 1);
        root.SetColumnSpan(actions, 2);
        Controls.Add(root);
        PopulateTree();
    }

    private static Button CreateButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(3, 5, 3, 3) };
        button.Click += click;
        return button;
    }

    private void AddTopLevel()
    {
        var item = new MenuItemDocument
        {
            Name = GetUniqueName("Menu"),
            Caption = "New Menu"
        };
        _items.Add(item);
        PopulateTree(item);
    }

    private void AddSubmenu()
    {
        var parent = _tree.SelectedNode?.Tag as MenuItemDocument;
        if (parent is null)
        {
            AddTopLevel();
            return;
        }

        var item = new MenuItemDocument
        {
            Name = GetUniqueName(parent.Name + "Item"),
            Caption = "New Item"
        };
        parent.Children.Add(item);
        PopulateTree(item);
    }

    private void RemoveSelected()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is not MenuItemDocument item)
            return;
        var owner = node.Parent?.Tag is MenuItemDocument parent ? parent.Children : _items;
        owner.Remove(item);
        _properties.SelectedObject = null;
        PopulateTree();
    }

    private string GetUniqueName(string prefix)
    {
        var names = EnumerateItems(_items).Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var name = $"{prefix}{index}";
            if (!names.Contains(name))
                return name;
        }
    }

    private void PopulateTree(MenuItemDocument? select = null)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var item in _items)
            _tree.Nodes.Add(CreateNode(item, select));
        _tree.EndUpdate();
        if (select is not null)
        {
            var selected = FindNode(_tree.Nodes, select);
            if (selected is not null)
            {
                _tree.SelectedNode = selected;
                selected.EnsureVisible();
                _properties.SelectedObject = select;
            }
        }
    }

    private static TreeNode CreateNode(MenuItemDocument item, MenuItemDocument? select)
    {
        var node = new TreeNode(GetDisplayCaption(item)) { Tag = item };
        foreach (var child in item.Children)
            node.Nodes.Add(CreateNode(child, select));
        if (item.Children.Count > 0)
            node.Expand();
        return node;
    }

    private void RefreshTreeLabels()
    {
        foreach (TreeNode node in _tree.Nodes)
            RefreshTreeLabel(node);
        _tree.Refresh();
    }

    private static void RefreshTreeLabel(TreeNode node)
    {
        if (node.Tag is MenuItemDocument item)
            node.Text = GetDisplayCaption(item);
        foreach (TreeNode child in node.Nodes)
            RefreshTreeLabel(child);
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, MenuItemDocument item)
    {
        foreach (TreeNode node in nodes)
        {
            if (ReferenceEquals(node.Tag, item))
                return node;
            var found = FindNode(node.Nodes, item);
            if (found is not null)
                return found;
        }
        return null;
    }

    internal static string GetDisplayCaption(MenuItemDocument item) =>
        string.IsNullOrEmpty(item.Caption) ? $"({item.Name})" : item.Caption;

    internal static IEnumerable<MenuItemDocument> EnumerateItems(IEnumerable<MenuItemDocument> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in EnumerateItems(item.Children))
                yield return child;
        }
    }
}
