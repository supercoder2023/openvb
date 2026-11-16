using System.Reflection;
using System.Windows.Forms;

namespace OpenVBStudio.IDE;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About OpenVB Studio";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 270);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var logo = new Label
        {
            Text = "VB",
            BackColor = Color.FromArgb(35, 91, 150),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(24, 24, 64, 64)
        };
        var title = new Label
        {
            Text = "OpenVB Studio",
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(105, 27)
        };
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionLabel = new Label
        {
            Text = $"Version {version?.ToString(3) ?? "1.0.0"}",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(107, 60)
        };
        var description = new Label
        {
            Text = "An open-source Windows development environment inspired by classic Visual Basic workflows.",
            AutoSize = false,
            Bounds = new Rectangle(24, 111, 432, 45)
        };
        var note = new Label
        {
            Text = "Language support and features are evolving. OpenVB Studio does not currently provide full VB6 compatibility or native executable compilation.",
            AutoSize = false,
            Bounds = new Rectangle(24, 164, 432, 48),
            ForeColor = SystemColors.GrayText
        };
        var close = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(88, 30),
            Location = new Point(368, 224)
        };

        Controls.AddRange([logo, title, versionLabel, description, note, close]);
        AcceptButton = close;
    }
}
