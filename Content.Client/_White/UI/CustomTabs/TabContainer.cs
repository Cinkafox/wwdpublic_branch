using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.Client._White.UI.CustomTabs;


public sealed class CustomTabContainer : Control
{
    [ViewVariables] private Dictionary<string, Control> _tabs = new Dictionary<string, Control>();

    private List<TabChild> _tabsC = [];
    [ViewVariables] public List<TabChild> Tabs
    {
        set
        {
            foreach (var child in value)
            {
                if(string.IsNullOrEmpty(child.TabName))
                    throw new ArgumentException("Tab child must have a name");

                if(child.ChildCount == 0)
                    throw new ArgumentException("Tab does not contain any control!");

                Logger.Debug("<<<> " + child.TabName);

                _tabs.Add(child.TabName, child.Value);
            }

            _tabsC = value;
        }

        get => _tabsC;
    }

    public T ShowControl<T>(string name) where T : Control
    {
        if(!_tabs.TryGetValue(name, out var control) || control is not T t)
            throw new ArgumentException("Tab does not contain control named " + name);
        SetChild(control);
        return t;
    }

    public void ShowControl(string name)
    {
        if(!_tabs.TryGetValue(name, out var control))
            throw new ArgumentException("Tab does not contain control named " + name);
        SetChild(control);
    }

    public void AddControl(string name, Control control)
    {
        _tabs.Add(name, control);
    }

    private void SetChild(Control child)
    {
        child.Orphan();
        Children.Clear();
        Children.Add(child);
        LayoutContainer.SetAnchorPreset(child, LayoutContainer.LayoutPreset.Wide);
    }
}

public sealed class TabChild : Control
{
    [ViewVariables] public string TabName { get; set; } = string.Empty;
    [ViewVariables] public Control Value { get; set; } = default!;
}

public sealed class CustomTabButton : ContainerButton
{
    [ViewVariables] public string TabName { get; set; } = string.Empty;

    [ViewVariables] public CustomTabContainer TabContainer { get; set; }= default!;

    public CustomTabButton()
    {
        OnPressed += OnOnPressed;
    }

    private void OnOnPressed(ButtonEventArgs obj)
    {
        TabContainer.ShowControl(TabName);
    }

    public void SetTabInvoker(CustomTabContainer tabContainer)
    {
        TabContainer = tabContainer;
    }
}
