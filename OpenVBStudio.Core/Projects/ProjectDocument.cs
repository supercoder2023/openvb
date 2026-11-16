using System.ComponentModel;

namespace OpenVBStudio.Core.Projects;

public sealed class ProjectDocument
{
    public string Name { get; set; } = "Project1";
    public List<FormDocument> Forms { get; set; } = [FormDocument.CreateDefault()];
}

public sealed class FormDocument
{
    public string Name { get; set; } = "Form1";
    public string Caption { get; set; } = "Form1";
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public List<ControlDocument> Controls { get; set; } = [];
    public List<MenuItemDocument> MenuItems { get; set; } = [];
    public string Code { get; set; } = "";

    public static FormDocument CreateDefault() => new();
}

public sealed class MenuItemDocument
{
    public string Name { get; set; } = "Menu1";
    public string Caption { get; set; } = "Menu";
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;
    [Browsable(false)]
    public List<MenuItemDocument> Children { get; set; } = [];
}

public sealed class ControlDocument
{
    public string Name { get; set; } = "Command1";
    public string Kind { get; set; } = "CommandButton";
    public string Caption { get; set; } = "Command1";
    public int Left { get; set; } = 24;
    public int Top { get; set; } = 24;
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 28;
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;
}
