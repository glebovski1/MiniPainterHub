namespace MiniPainterHub.WebApp.Layout;

public sealed class UserPanelState
{
    public bool IsOpen { get; private set; }
    public bool IsDesktopCollapsed { get; private set; }

    public event Action? OnChange;

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        OnChange?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        OnChange?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void SetDesktopCollapsed(bool isCollapsed)
    {
        if (IsDesktopCollapsed == isCollapsed)
        {
            return;
        }

        IsDesktopCollapsed = isCollapsed;
        OnChange?.Invoke();
    }

    public void ToggleDesktopCollapsed()
    {
        SetDesktopCollapsed(!IsDesktopCollapsed);
    }
}
