using OpenVBStudio.Core.Projects;

namespace OpenVBStudio.Tests;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task SavesAndRestoresFormLayoutAndCode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ovbproj");
        var project = new ProjectDocument
        {
            Name = "Sample",
            Forms =
            [
                new FormDocument
                {
                    Name = "MainForm",
                    Caption = "Hello",
                    Width = 800,
                    Height = 600,
                    Code = "Private Sub Command1_Click()",
                    Controls =
                    [
                        new ControlDocument
                        {
                            Name = "Command1",
                            Kind = "CommandButton",
                            Caption = "Click",
                            Left = 42,
                            Top = 24,
                            Width = 120,
                            Height = 32
                        }
                    ],
                    MenuItems =
                    [
                        new MenuItemDocument
                        {
                            Name = "FileMenu",
                            Caption = "&File",
                            Children =
                            [
                                new MenuItemDocument
                                {
                                    Name = "ExitMenu",
                                    Caption = "E&xit"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        try
        {
            var service = new ProjectService();
            await service.SaveAsync(project, path);
            var restored = await service.OpenAsync(path);

            Assert.Equal("Sample", restored.Name);
            Assert.Equal(800, restored.Forms[0].Width);
            Assert.Equal("Private Sub Command1_Click()", restored.Forms[0].Code);
            Assert.Equal(42, restored.Forms[0].Controls[0].Left);
            Assert.Equal("Click", restored.Forms[0].Controls[0].Caption);
            Assert.Equal("&File", restored.Forms[0].MenuItems[0].Caption);
            Assert.Equal("ExitMenu", restored.Forms[0].MenuItems[0].Children[0].Name);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsDuplicateControlNames()
    {
        var project = new ProjectDocument();
        project.Forms[0].Controls.Add(new ControlDocument { Name = "Text1" });
        project.Forms[0].Controls.Add(new ControlDocument { Name = "text1" });
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ovbproj");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => new ProjectService().SaveAsync(project, path));

        Assert.Contains("duplicate control name", exception.Message);
    }

    [Fact]
    public async Task RejectsDuplicateNamesAcrossControlsAndMenus()
    {
        var project = new ProjectDocument();
        project.Forms[0].Controls.Add(new ControlDocument { Name = "FileMenu" });
        project.Forms[0].MenuItems.Add(new MenuItemDocument { Name = "filemenu" });
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ovbproj");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => new ProjectService().SaveAsync(project, path));

        Assert.Contains("duplicate control or menu name", exception.Message);
    }
}
