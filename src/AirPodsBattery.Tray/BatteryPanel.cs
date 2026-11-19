using System.Drawing;
using Forms = System.Windows.Forms;

namespace AirPodsBattery.Tray;

internal sealed class BatteryPanel : Forms.ToolStripDropDown
{
    private readonly Forms.Label _device;
    private readonly Forms.Label _percentage;
    private readonly Forms.Label _status;
    private readonly Forms.Label _details;
    private Point _dismissedAt;
    private long _dismissedTime;

    public BatteryPanel()
    {
        AutoClose = true;
        Padding = Forms.Padding.Empty;
        var content = new Forms.TableLayoutPanel
        {
            Size = new Size(300, 210),
            Padding = new Forms.Padding(20, 16, 20, 16),
            BackColor = Color.FromArgb(24, 28, 36),
            ColumnCount = 1,
            RowCount = 4,
            AccessibleName = "AirPods battery"
        };
        _device = AddLabel(content, 12, FontStyle.Bold, Color.White);
        _percentage = AddLabel(content, 36, FontStyle.Bold, Color.White);
        _status = AddLabel(content, 10, FontStyle.Regular, Color.FromArgb(190, 202, 220));
        _details = AddLabel(content, 10, FontStyle.Regular, Color.FromArgb(190, 202, 220));
        Items.Add(new Forms.ToolStripControlHost(content)
        {
            Margin = Forms.Padding.Empty,
            Padding = Forms.Padding.Empty,
            AutoSize = false,
            Size = content.Size
        });
        Closed += (_, e) =>
        {
            if (e.CloseReason == Forms.ToolStripDropDownCloseReason.AppClicked)
            {
                _dismissedAt = Forms.Cursor.Position;
                _dismissedTime = Environment.TickCount64;
            }
        };
    }

    private static Forms.Label AddLabel(Forms.TableLayoutPanel content, float size, FontStyle style, Color color)
    {
        var label = new Forms.Label
        {
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            Margin = new Forms.Padding(0, 0, 0, 6)
        };
        content.Controls.Add(label);
        return label;
    }

    public void UpdateState(BatteryPanelState state)
    {
        _device.Text = state.Device;
        _percentage.Text = state.Percentage;
        _status.Text = state.Status;
        _details.Text = state.Details;
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
            return;
        }

        var cursor = Forms.Cursor.Position;
        // The native popup can dismiss on mouse-down before NotifyIcon receives mouse-up.
        if (Environment.TickCount64 - _dismissedTime < 500 &&
            Math.Abs(cursor.X - _dismissedAt.X) < 4 && Math.Abs(cursor.Y - _dismissedAt.Y) < 4)
            return;

        var area = Forms.Screen.FromPoint(cursor).WorkingArea;
        var x = Math.Clamp(cursor.X - Width / 2, area.Left, Math.Max(area.Left, area.Right - Width));
        var y = cursor.Y - Height - 12;
        if (y < area.Top)
            y = cursor.Y + 12;
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Show(new Point(x, y));
    }
}
