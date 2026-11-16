using System.Text.Json;

namespace OpenVBStudio.Core.Projects;

public sealed class ProjectService
{
    private static readonly HashSet<string> SupportedControlKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "TextBox", "CommandButton", "CheckBox", "OptionButton", "ComboBox", "ListBox", "Frame"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveAsync(ProjectDocument project, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Validate(project);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, JsonOptions, cancellationToken);
    }

    public async Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<ProjectDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The project file is empty or invalid.");
        Validate(project);
        return project;
    }

    private static void Validate(ProjectDocument project)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
            throw new InvalidDataException("A project must have a name.");

        if (project.Forms is null || project.Forms.Count == 0)
            throw new InvalidDataException("A project must contain at least one form.");

        var duplicateForm = project.Forms.Where(form => form is not null)
            .GroupBy(form => form.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateForm is not null)
            throw new InvalidDataException($"The project contains duplicate form name '{duplicateForm.Key}'.");

        foreach (var form in project.Forms)
        {
            if (form is null)
                throw new InvalidDataException("A project cannot contain a null form.");
            if (string.IsNullOrWhiteSpace(form.Name) || form.Width <= 0 || form.Height <= 0)
                throw new InvalidDataException("Each form needs a name and positive dimensions.");
            if (form.Controls is null)
                throw new InvalidDataException($"Form '{form.Name}' must have a controls collection.");
            if (form.MenuItems is null)
                throw new InvalidDataException($"Form '{form.Name}' must have a menu items collection.");

            if (form.Controls.Any(control => control is null || string.IsNullOrWhiteSpace(control.Name) || string.IsNullOrWhiteSpace(control.Kind)))
                throw new InvalidDataException($"Form '{form.Name}' contains a control without a name or kind.");
            if (form.Controls.Any(control => !SupportedControlKinds.Contains(control.Kind)))
                throw new InvalidDataException($"Form '{form.Name}' contains an unsupported control kind.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var control in form.Controls)
            {
                if (!names.Add(control.Name))
                    throw new InvalidDataException($"Form '{form.Name}' contains duplicate control name '{control.Name}'.");
            }
            ValidateMenuItems(form.Name, form.MenuItems, names);
        }
    }

    private static void ValidateMenuItems(string formName, IEnumerable<MenuItemDocument> items, HashSet<string> names)
    {
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Caption) || item.Children is null)
                throw new InvalidDataException($"Form '{formName}' contains an invalid menu item.");
            if (!names.Add(item.Name))
                throw new InvalidDataException($"Form '{formName}' contains duplicate control or menu name '{item.Name}'.");
            ValidateMenuItems(formName, item.Children, names);
        }
    }
}
