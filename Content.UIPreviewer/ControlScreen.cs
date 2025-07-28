using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.UIPreviewer;


public sealed class ControlScreen : UIScreen
{
    private readonly PanelContainer _controlContainer;
    private readonly BoxContainer _messageContainer;
    public ControlScreen()
    {
        var panel = new PanelContainer();
        AddChild(panel);
        panel.AddChild(_controlContainer = new PanelContainer());
        panel.AddChild(_messageContainer = new BoxContainer());
        SetAnchorPreset(_controlContainer, LayoutPreset.Wide);
        SetAnchorPreset(_messageContainer, LayoutPreset.Wide);
        SetAnchorPreset(panel, LayoutPreset.Wide);
    }
    public void SetControl(Control control)
    {
        _controlContainer.Children.Clear();
        _controlContainer.AddChild(control);
        SetAnchorPreset(control, LayoutPreset.Wide);
    }

    public void AddMessage(string message)
    {
        _messageContainer.AddChild(
            new Label()
            {
                Text = message,
            });
    }

    public void ClearMessages()
    {
        _messageContainer.Children.Clear();
    }
}
