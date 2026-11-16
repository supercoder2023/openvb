using System.Drawing;
using System.Windows.Forms;
using OpenVBStudio.Core.Language;
using OpenVBStudio.Core.Projects;

namespace OpenVBStudio.IDE;

internal sealed class MainForm : Form
{
    private readonly ProjectService _projectService = new();
    private readonly BasicInterpreter _interpreter = new();
    private readonly TreeView _projectTree = new();
    private readonly ToolboxPalette _toolbox = new();
    private readonly PropertyGrid _propertyGrid = new();
    private readonly TabControl _editorTabs = new();
    private readonly RichTextBox _codeEditor = new();
    private readonly Panel _designerViewport = new();
    private readonly Panel _formCanvas = new();
    private readonly Panel _menuPreview = new();
    private readonly ListBox _outputList = new();
    private readonly ListBox _errorList = new();
    private readonly ToolStripStatusLabel _statusLabel = new("Ready");
    private readonly ToolStripStatusLabel _projectPathLabel = new("");
    private ProjectDocument _project = new();
    private FormDocument _activeForm;
    private ControlDocument? _selectedControl;
    private Control? _draggedControl;
    private Point _dragStart;
    private Rectangle _initialBounds;
    private bool _resizing;
    private bool _isLoadingDocument;
    private string? _projectPath;
    private readonly Dictionary<ControlDocument, (long Timestamp, Point Location)> _lastControlClicks = [];

    public MainForm()
    {
        _activeForm = _project.Forms[0];
        Text = "OpenVB Studio";
        Width = 1440;
        Height = 900;
        MinimumSize = new Size(1050, 680);
        KeyPreview = true;

        BuildLayout();
        WireEvents();
        RefreshProjectTree();
        RefreshDesigner();
    }

    private void OpenMenuEditor()
    {
        SaveActiveCode();
        using var editor = new MenuEditorForm(_activeForm.MenuItems);
        editor.ShowDialog(this);
        RefreshDesigner();
        SetStatus("Form menu updated");
    }

    private void BuildLayout()
    {
        var menu = BuildMenu();
        var toolbar = BuildToolbar();
        var status = new StatusStrip();
        status.Items.Add(_statusLabel);
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(_projectPathLabel);
        Controls.Add(BuildMainLayout());
        Controls.Add(status);
        Controls.Add(toolbar);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("New Project", null, (_, _) => NewProject());
        file.DropDownItems.Add("Open Project...", null, async (_, _) => await OpenProjectAsync());
        file.DropDownItems.Add("Save Project", null, async (_, _) => await SaveProjectAsync());
        file.DropDownItems.Add("Save Project As...", null, async (_, _) => await SaveProjectAsAsync());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());

        var edit = new ToolStripMenuItem("&Edit");
        edit.DropDownItems.Add("Delete Selected Control", null, (_, _) => DeleteSelectedControl());
        edit.DropDownItems.Add("Rename Form...", null, (_, _) => RenameActiveForm());

        var view = new ToolStripMenuItem("&View");
        view.DropDownItems.Add("Designer", null, (_, _) => _editorTabs.SelectedIndex = 0);
        view.DropDownItems.Add("Code", null, (_, _) => _editorTabs.SelectedIndex = 1);

        var project = new ToolStripMenuItem("&Project");
        project.DropDownItems.Add("Add Form", null, (_, _) => AddForm());
        project.DropDownItems.Add("Menu Editor...", null, (_, _) => OpenMenuEditor());

        var run = new ToolStripMenuItem("&Run");
        run.DropDownItems.Add("Start", null, (_, _) => RunProject());
        run.DropDownItems.Add("View Output", null, (_, _) => _outputList.Focus());

        var tools = new ToolStripMenuItem("&Tools");
        tools.DropDownItems.Add("Add Selected Toolbox Control", null, (_, _) => AddSelectedToolboxControl());

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("About OpenVB Studio", null, (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        });

        menu.Items.AddRange([file, edit, view, project, run, tools, help]);
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolbar.Items.Add(new ToolStripButton("New", null, (_, _) => NewProject()));
        toolbar.Items.Add(new ToolStripButton("Open", null, async (_, _) => await OpenProjectAsync()));
        toolbar.Items.Add(new ToolStripButton("Save", null, async (_, _) => await SaveProjectAsync()));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Add Form", null, (_, _) => AddForm()));
        toolbar.Items.Add(new ToolStripButton("Add Control", null, (_, _) => AddSelectedToolboxControl()));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Run", null, (_, _) => RunProject()));
        return toolbar;
    }

    private Control BuildMainLayout()
    {
        var layout = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1200, 800),
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 238,
            Panel1MinSize = 180,
            Panel2MinSize = 560
        };
        _menuPreview.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TryPlaceSelectedTool(new Point(e.X, e.Y - 24));
        };
        var explorerAndToolbox = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(238, 760),
            Orientation = Orientation.Horizontal,
            SplitterDistance = 310,
            Panel1MinSize = 150,
            Panel2MinSize = 160
        };

        var explorerPanel = MakeLabeledPanel("Project Explorer", _projectTree);
        _projectTree.Dock = DockStyle.Fill;
        _projectTree.HideSelection = false;
        _projectTree.LabelEdit = false;
        var toolboxPanel = MakeLabeledPanel("Toolbox", _toolbox);
        _toolbox.Dock = DockStyle.Fill;
        explorerAndToolbox.Panel1.Controls.Add(explorerPanel);
        explorerAndToolbox.Panel2.Controls.Add(toolboxPanel);
        layout.Panel1.Controls.Add(explorerAndToolbox);

        var workAndOutput = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(960, 760),
            Orientation = Orientation.Horizontal,
            SplitterDistance = 590,
            Panel1MinSize = 300,
            Panel2MinSize = 100
        };

        var workspaceAndProperties = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1200, 760),
            SplitterDistance = 780,
            Panel1MinSize = 350,
            Panel2MinSize = 190
        };
        _editorTabs.Dock = DockStyle.Fill;
        var designerTab = new TabPage("Form Designer");
        _designerViewport.Dock = DockStyle.Fill;
        _designerViewport.AutoScroll = true;
        _designerViewport.BackColor = SystemColors.AppWorkspace;
        _formCanvas.BackColor = Color.White;
        _formCanvas.BorderStyle = BorderStyle.FixedSingle;
        _formCanvas.Location = new Point(20, 20);
        _menuPreview.BackColor = SystemColors.MenuBar;
        _menuPreview.Paint += PaintMenuPreview;
        _menuPreview.DoubleClick += (_, _) => OpenMenuEditor();
        _designerViewport.Controls.Add(_formCanvas);
        designerTab.Controls.Add(_designerViewport);

        var codeTab = new TabPage("Code");
        _codeEditor.Dock = DockStyle.Fill;
        _codeEditor.Font = new Font("Consolas", 10);
        _codeEditor.AcceptsTab = true;
        _codeEditor.WordWrap = false;
        _codeEditor.DetectUrls = false;
        codeTab.Controls.Add(_codeEditor);
        _editorTabs.TabPages.AddRange([designerTab, codeTab]);
        workspaceAndProperties.Panel1.Controls.Add(_editorTabs);

        _propertyGrid.Dock = DockStyle.Fill;
        _propertyGrid.ToolbarVisible = false;
        workspaceAndProperties.Panel2.Controls.Add(MakeLabeledPanel("Properties", _propertyGrid));
        workAndOutput.Panel1.Controls.Add(workspaceAndProperties);

        var outputTabs = new TabControl { Dock = DockStyle.Fill };
        var outputPage = new TabPage("Output");
        _outputList.Dock = DockStyle.Fill;
        outputPage.Controls.Add(_outputList);
        var errorsPage = new TabPage("Error List");
        _errorList.Dock = DockStyle.Fill;
        errorsPage.Controls.Add(_errorList);
        outputTabs.TabPages.AddRange([outputPage, errorsPage]);
        workAndOutput.Panel2.Controls.Add(outputTabs);
        layout.Panel2.Controls.Add(workAndOutput);
        return layout;
    }

    private static Control MakeLabeledPanel(string title, Control content)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6, 14, 6, 6) };
        group.Controls.Add(content);
        return group;
    }

    private void WireEvents()
    {
        _projectTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is FormDocument form)
            {
                SaveActiveCode();
                _activeForm = form;
                _selectedControl = null;
                _propertyGrid.SelectedObject = form;
                LoadActiveCode();
                RefreshDesigner();
            }
            else if (e.Node?.Tag is ControlDocument control)
            {
                SelectControl(control);
            }
        };
        _formCanvas.AllowDrop = true;
        _formCanvas.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.StringFormat) == true)
                e.Effect = DragDropEffects.Copy;
        };
        _formCanvas.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.StringFormat) is string kind && kind != "Pointer")
                AddControl(kind, _formCanvas.PointToClient(new Point(e.X, e.Y)));
        };
        _formCanvas.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                var menuHeight = _activeForm.MenuItems.Any(item => item.Visible) ? 24 : 0;
                if (!TryPlaceSelectedTool(new Point(e.X, e.Y - menuHeight)))
                    SelectControl(null);
            }
        };
        _propertyGrid.PropertyValueChanged += (_, _) =>
        {
            if (!_isLoadingDocument)
            {
                RefreshDesigner();
                RefreshProjectTree();
            }
        };
        _codeEditor.TextChanged += (_, _) =>
        {
            if (!_isLoadingDocument)
                SetStatus("Code modified");
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && _editorTabs.SelectedIndex == 0 && !_codeEditor.ContainsFocus)
                DeleteSelectedControl();
            else if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                _ = SaveProjectAsync();
            }
            else if (e.KeyCode == Keys.F5)
                RunProject();
        };
    }

    private void NewProject()
    {
        if (HasUnsavedCode() && MessageBox.Show(this, "Discard current code changes?", "New Project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _project = new ProjectDocument();
        _activeForm = _project.Forms[0];
        _projectPath = null;
        _selectedControl = null;
        _projectPathLabel.Text = "Unsaved project";
        _outputList.Items.Clear();
        _errorList.Items.Clear();
        LoadActiveCode();
        RefreshProjectTree();
        RefreshDesigner();
        SetStatus("New project");
    }

    private async Task OpenProjectAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "OpenVB project (*.ovbproj)|*.ovbproj|JSON files (*.json)|*.json",
            Title = "Open Project"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _project = await _projectService.OpenAsync(dialog.FileName);
            _projectPath = dialog.FileName;
            _activeForm = _project.Forms[0];
            _selectedControl = null;
            _projectPathLabel.Text = Path.GetFileName(_projectPath);
            LoadActiveCode();
            RefreshProjectTree();
            RefreshDesigner();
            SetStatus($"Opened {_project.Name}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            ReportError($"Could not open project: {exception.Message}");
        }
    }

    private async Task SaveProjectAsync()
    {
        if (_projectPath is null)
        {
            await SaveProjectAsAsync();
            return;
        }
        await SaveToPathAsync(_projectPath);
    }

    private async Task SaveProjectAsAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "OpenVB project (*.ovbproj)|*.ovbproj",
            DefaultExt = "ovbproj",
            AddExtension = true,
            FileName = $"{_project.Name}.ovbproj",
            Title = "Save Project"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await SaveToPathAsync(dialog.FileName);
    }

    private async Task SaveToPathAsync(string path)
    {
        try
        {
            SaveActiveCode();
            await _projectService.SaveAsync(_project, path);
            _projectPath = path;
            _projectPathLabel.Text = Path.GetFileName(path);
            SetStatus($"Saved {_project.Name}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ReportError($"Could not save project: {exception.Message}");
        }
    }

    private void AddForm()
    {
        var index = 1;
        while (_project.Forms.Any(form => form.Name.Equals($"Form{index}", StringComparison.OrdinalIgnoreCase)))
            index++;
        var form = new FormDocument { Name = $"Form{index}", Caption = $"Form{index}" };
        _project.Forms.Add(form);
        SaveActiveCode();
        _activeForm = form;
        _selectedControl = null;
        LoadActiveCode();
        RefreshProjectTree();
        RefreshDesigner();
        SetStatus($"Added {form.Name}");
    }

    private void RenameActiveForm()
    {
        var name = PromptForText("Rename Form", "Form name:", _activeForm.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = name.Trim();
        if (_project.Forms.Any(form => !ReferenceEquals(form, _activeForm) && form.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "A form with that name already exists.", "Rename Form", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var previousName = _activeForm.Name;
        _activeForm.Name = name.Trim();
        if (_activeForm.Caption == previousName || string.IsNullOrWhiteSpace(_activeForm.Caption))
            _activeForm.Caption = _activeForm.Name;
        RefreshProjectTree();
        RefreshDesigner();
    }

    private void AddSelectedToolboxControl()
    {
        var kind = _toolbox.SelectedKind;
        if (kind is null or "Pointer")
        {
            SetStatus("Select a control in the Toolbox first");
            return;
        }
        AddControl(kind, new Point(24 + (_activeForm.Controls.Count % 5) * 18, 24 + (_activeForm.Controls.Count % 5) * 18));
    }

    private void AddControl(string kind, Point location)
    {
        var baseName = kind switch
        {
            "CommandButton" => "Command",
            "TextBox" => "Text",
            "CheckBox" => "Check",
            "OptionButton" => "Option",
            "ComboBox" => "Combo",
            "ListBox" => "List",
            "Frame" => "Frame",
            _ => kind
        };
        var number = 1;
        while (_activeForm.Controls.Any(control => control.Name.Equals($"{baseName}{number}", StringComparison.OrdinalIgnoreCase)))
            number++;
        var control = new ControlDocument
        {
            Name = $"{baseName}{number}",
            Kind = kind,
            Caption = kind == "TextBox" ? "" : $"{baseName}{number}",
            Left = Math.Max(0, location.X),
            Top = Math.Max(0, location.Y),
            Width = kind == "Frame" ? 160 : kind == "TextBox" ? 140 : 100,
            Height = kind == "Frame" ? 100 : kind == "TextBox" ? 24 : 28
        };
        _activeForm.Controls.Add(control);
        RefreshProjectTree();
        RefreshDesigner();
        SelectControl(control);
        SetStatus($"Added {control.Name}");
    }

    private void RefreshProjectTree()
    {
        _projectTree.BeginUpdate();
        _projectTree.Nodes.Clear();
        var root = new TreeNode(_project.Name) { Tag = _project };
        foreach (var form in _project.Forms)
        {
            var formNode = new TreeNode(form.Name) { Tag = form };
            foreach (var control in form.Controls)
                formNode.Nodes.Add(new TreeNode(control.Name) { Tag = control });
            root.Nodes.Add(formNode);
        }
        _projectTree.Nodes.Add(root);
        root.Expand();
        var activeNode = _projectTree.Nodes[0].Nodes.Cast<TreeNode>().FirstOrDefault(node => ReferenceEquals(node.Tag, _activeForm));
        if (activeNode is not null)
            activeNode.Expand();
        _projectTree.EndUpdate();
    }

    private void RefreshDesigner()
    {
        _formCanvas.SuspendLayout();
        _formCanvas.Controls.Clear();
        var menuHeight = _activeForm.MenuItems.Any(item => item.Visible) ? 24 : 0;
        _formCanvas.Size = new Size(_activeForm.Width, _activeForm.Height + menuHeight);
        if (menuHeight > 0)
        {
            _menuPreview.SetBounds(0, 0, _activeForm.Width, menuHeight);
            _menuPreview.Visible = true;
            _formCanvas.Controls.Add(_menuPreview);
        }
        else
        {
            _menuPreview.Visible = false;
        }
        foreach (var model in _activeForm.Controls.Where(item => item.Visible))
        {
            var control = CreateControl(model);
            control.SetBounds(model.Left, model.Top + menuHeight, model.Width, model.Height);
            control.Enabled = model.Enabled;
            control.Tag = model;
            control.MouseDown += DesignerControlMouseDown;
            control.MouseMove += DesignerControlMouseMove;
            control.MouseUp += DesignerControlMouseUp;
            control.Paint += (_, e) =>
            {
                if (ReferenceEquals(_selectedControl, model))
                {
                    ControlPaint.DrawBorder(e.Graphics, control.ClientRectangle, Color.DodgerBlue, ButtonBorderStyle.Dashed);
                    e.Graphics.FillRectangle(Brushes.DodgerBlue, control.Width - 7, control.Height - 7, 6, 6);
                }
            };
            _formCanvas.Controls.Add(control);
        }
        _formCanvas.ResumeLayout();
        _formCanvas.Invalidate();
    }

    private void PaintMenuPreview(object? sender, PaintEventArgs e)
    {
        var x = 8;
        foreach (var item in _activeForm.MenuItems.Where(item => item.Visible))
        {
            var caption = item.Caption.Replace("&", "", StringComparison.Ordinal);
            var color = item.Enabled ? SystemColors.MenuText : SystemColors.GrayText;
            var size = TextRenderer.MeasureText(e.Graphics, caption, Font);
            TextRenderer.DrawText(e.Graphics, caption, Font, new Point(x, 4), color);
            x += size.Width + 22;
        }
        using var border = new Pen(SystemColors.ControlDark);
        e.Graphics.DrawLine(border, 0, _menuPreview.Height - 1, _menuPreview.Width, _menuPreview.Height - 1);
    }

    private static Control CreateControl(ControlDocument model)
    {
        Control control = model.Kind switch
        {
            "Label" => new Label { TextAlign = ContentAlignment.MiddleLeft, AutoSize = false },
            "TextBox" => new TextBox(),
            "CommandButton" => new Button(),
            "CheckBox" => new CheckBox { AutoSize = false },
            "OptionButton" => new RadioButton { AutoSize = false },
            "ComboBox" => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList },
            "ListBox" => new ListBox(),
            "Frame" => new GroupBox(),
            _ => new Label()
        };
        control.Name = model.Name;
        control.Text = model.Caption;
        return control;
    }

    private void SelectControl(ControlDocument? model)
    {
        _selectedControl = model;
        _propertyGrid.SelectedObject = (object?)model ?? _activeForm;
        _formCanvas.Invalidate();
        if (model is not null)
            SetStatus($"Selected {model.Name}");
    }

    private void DesignerControlMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not Control control || control.Tag is not ControlDocument model)
            return;
        var menuHeight = _activeForm.MenuItems.Any(item => item.Visible) ? 24 : 0;
        var designerLocation = _formCanvas.PointToClient(control.PointToScreen(e.Location));
        if (e.Button == MouseButtons.Left && TryPlaceSelectedTool(new Point(designerLocation.X, designerLocation.Y - menuHeight)))
            return;

        SelectControl(model);
        if (e.Button == MouseButtons.Left && model.Kind == "CommandButton")
        {
            var now = Environment.TickCount64;
            if (_lastControlClicks.TryGetValue(model, out var previousClick)
                && now - previousClick.Timestamp <= SystemInformation.DoubleClickTime
                && Math.Abs(e.X - previousClick.Location.X) <= SystemInformation.DoubleClickSize.Width
                && Math.Abs(e.Y - previousClick.Location.Y) <= SystemInformation.DoubleClickSize.Height)
            {
                _lastControlClicks.Remove(model);
                OpenClickEvent(model);
                return;
            }
            _lastControlClicks[model] = (now, e.Location);
        }
        _draggedControl = control;
        _dragStart = new Point(control.Left + e.X, control.Top + e.Y);
        _initialBounds = control.Bounds;
        _resizing = e.X >= control.Width - 10 && e.Y >= control.Height - 10;
        control.Capture = true;
    }

    private bool TryPlaceSelectedTool(Point location)
    {
        var kind = _toolbox.SelectedKind;
        if (kind is null or "Pointer")
            return false;

        AddControl(kind, location);
        _toolbox.SelectPointer();
        return true;
    }

    private void DesignerControlMouseMove(object? sender, MouseEventArgs e)
    {
        if (_draggedControl is null || sender is not Control control || !control.Capture)
            return;
        var current = _formCanvas.PointToClient(control.PointToScreen(e.Location));
        var deltaX = current.X - _dragStart.X;
        var deltaY = current.Y - _dragStart.Y;
        if (_resizing)
        {
            control.Width = Math.Max(20, _initialBounds.Width + deltaX);
            control.Height = Math.Max(18, _initialBounds.Height + deltaY);
            _selectedControl!.Width = control.Width;
            _selectedControl.Height = control.Height;
        }
        else
        {
            control.Left = Math.Max(0, _initialBounds.Left + deltaX);
            control.Top = Math.Max(0, _initialBounds.Top + deltaY);
            _selectedControl!.Left = control.Left;
            _selectedControl.Top = control.Top;
        }
        _propertyGrid.Refresh();
    }

    private void DesignerControlMouseUp(object? sender, MouseEventArgs e)
    {
        if (_draggedControl is not null)
            _draggedControl.Capture = false;
        _draggedControl = null;
        _resizing = false;
    }

    private void DeleteSelectedControl()
    {
        if (_selectedControl is null)
            return;
        _activeForm.Controls.Remove(_selectedControl);
        _selectedControl = null;
        _propertyGrid.SelectedObject = _activeForm;
        RefreshProjectTree();
        RefreshDesigner();
        SetStatus("Control deleted");
    }

    private void OpenClickEvent(ControlDocument control)
    {
        SaveActiveCode();
        var procedureName = $"{control.Name}_Click";
        if (!_activeForm.Code.Contains($"Sub {procedureName}", StringComparison.OrdinalIgnoreCase))
        {
            if (_activeForm.Code.Length > 0 && !_activeForm.Code.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                _activeForm.Code += Environment.NewLine;
            _activeForm.Code += $"Private Sub {procedureName}(){Environment.NewLine}    MsgBox \"Hello World\"{Environment.NewLine}End Sub{Environment.NewLine}";
        }
        LoadActiveCode();
        _editorTabs.SelectedIndex = 1;
        SetStatus($"Editing {procedureName}");
    }

    private void RunProject()
    {
        SaveActiveCode();
        var runtimeForms = new Dictionary<string, Form>(StringComparer.OrdinalIgnoreCase);
        foreach (var formDocument in _project.Forms)
        {
            runtimeForms.Add(formDocument.Name, CreateRuntimeForm(formDocument, runtimeForms));
        }

        var startupForm = runtimeForms[_activeForm.Name];
        SetStatus($"Running {_activeForm.Name}");
        try
        {
            startupForm.ShowDialog(this);
        }
        finally
        {
            foreach (var form in runtimeForms.Values)
                form.Dispose();
        }
        SetStatus("Ready");
    }

    private Form CreateRuntimeForm(FormDocument document, IReadOnlyDictionary<string, Form> runtimeForms)
    {
        var runningForm = new Form
        {
            Name = document.Name,
            Text = document.Caption,
            ClientSize = new Size(document.Width, document.Height),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable
        };
        var visibleMenus = document.MenuItems.Where(item => item.Visible).ToArray();
        if (visibleMenus.Length > 0)
        {
            var menuStrip = new MenuStrip();
            foreach (var menu in visibleMenus)
                menuStrip.Items.Add(CreateRuntimeMenuItem(menu, document, runningForm, runtimeForms));
            runningForm.MainMenuStrip = menuStrip;
            runningForm.Controls.Add(menuStrip);
        }
        foreach (var model in document.Controls.Where(control => control.Visible))
        {
            var control = CreateControl(model);
            control.SetBounds(model.Left, model.Top, model.Width, model.Height);
            control.Enabled = model.Enabled;
            if (control is Button button)
            {
                button.Click += (_, _) => ExecuteRuntimeEvent(document, runningForm, runtimeForms, $"{model.Name}_Click");
            }
            runningForm.Controls.Add(control);
        }
        return runningForm;
    }

    private ToolStripMenuItem CreateRuntimeMenuItem(
        MenuItemDocument model,
        FormDocument document,
        Form runningForm,
        IReadOnlyDictionary<string, Form> runtimeForms)
    {
        var item = new ToolStripMenuItem(model.Caption)
        {
            Name = model.Name,
            Enabled = model.Enabled,
            Visible = model.Visible
        };
        item.Click += (_, _) => ExecuteRuntimeEvent(document, runningForm, runtimeForms, $"{model.Name}_Click");
        foreach (var child in model.Children.Where(child => child.Visible))
            item.DropDownItems.Add(CreateRuntimeMenuItem(child, document, runningForm, runtimeForms));
        return item;
    }

    private void ExecuteRuntimeEvent(
        FormDocument document,
        Form runningForm,
        IReadOnlyDictionary<string, Form> runtimeForms,
        string procedureName)
    {
        try
        {
            _interpreter.Execute(
                document.Code,
                procedureName,
                message => MessageBox.Show(runningForm, message, document.Caption, MessageBoxButtons.OK, MessageBoxIcon.Information),
                formName =>
                {
                    if (!runtimeForms.TryGetValue(formName, out var targetForm))
                        throw new InvalidOperationException($"Form '{formName}' does not exist in this project.");
                    if (!targetForm.Visible)
                        targetForm.Show(runningForm);
                },
                (controlName, propertyName) => ReadRuntimeControlProperty(runningForm, controlName, propertyName));
            LogOutput($"Executed {procedureName}");
        }
        catch (Exception exception)
        {
            ReportError($"{procedureName}: {exception.Message}");
            MessageBox.Show(runningForm, exception.Message, "BASIC Runtime Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static object? ReadRuntimeControlProperty(Form runningForm, string controlName, string propertyName)
    {
        var control = runningForm.Controls.Find(controlName, true).FirstOrDefault()
            ?? throw new InvalidOperationException($"Control '{controlName}' does not exist on form '{runningForm.Name}'.");

        return propertyName.ToUpperInvariant() switch
        {
            "TEXT" when control is TextBox => control.Text,
            "CAPTION" when control is Label or Button or CheckBox or RadioButton or GroupBox => control.Text,
            "ENABLED" => control.Enabled,
            "VISIBLE" => control.Visible,
            "NAME" => control.Name,
            "TEXT" or "CAPTION" => throw new InvalidOperationException(
                $"Property '{propertyName}' is not supported for control '{controlName}'."),
            _ => throw new InvalidOperationException($"Property '{propertyName}' is not supported for control '{controlName}'.")
        };
    }

    private void SaveActiveCode() => _activeForm.Code = _codeEditor.Text;

    private void LoadActiveCode()
    {
        _isLoadingDocument = true;
        _codeEditor.Text = _activeForm.Code;
        _propertyGrid.SelectedObject = (object?)_selectedControl ?? _activeForm;
        _isLoadingDocument = false;
    }

    private bool HasUnsavedCode() => !string.Equals(_codeEditor.Text, _activeForm.Code, StringComparison.Ordinal);

    private void ReportError(string message)
    {
        _errorList.Items.Add(message);
        LogOutput(message);
        SetStatus("An error occurred");
    }

    private void LogOutput(string message)
    {
        _outputList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        _outputList.TopIndex = _outputList.Items.Count - 1;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private static string? PromptForText(string title, string prompt, string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 110),
            MinimizeBox = false,
            MaximizeBox = false
        };
        var label = new Label { Text = prompt, Left = 12, Top = 12, AutoSize = true };
        var textBox = new TextBox { Text = initialValue, Left = 12, Top = 35, Width = 336 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 192, Top = 70, Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 273, Top = 70, Width = 75 };
        dialog.Controls.AddRange([label, textBox, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
}
