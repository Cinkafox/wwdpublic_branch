using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.UIPreviewer;


public sealed class ControlScreen : UIScreen
{
    private readonly PanelContainer _controlContainer;
    private readonly BoxContainer _messageContainer;
    public ControlScreen()
    {
        var stackPanel = new BoxContainer();
        AddChild(stackPanel);
        stackPanel.AddChild(_controlContainer = new PanelContainer());
        stackPanel.AddChild(_messageContainer = new BoxContainer());
    }
    public void SetControl(Control control)
    {
        _controlContainer.Children.Clear();
        _controlContainer.AddChild(control);
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
