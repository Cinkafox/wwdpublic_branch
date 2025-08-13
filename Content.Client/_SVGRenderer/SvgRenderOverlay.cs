using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;


namespace Content.Client._SVGRenderer;


public sealed class SvgRenderOverlay : Overlay
{
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    private readonly SvgDocument _svg;

    public SvgRenderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _svg = SvgDocument.Load(_resourceManager.ContentFileReadText(new ResPath("/silly.svg")).ReadToEnd());
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var g = args.DrawingHandle;
        var p = _uiManager.MousePositionScaled.Position - new Vector2(375, 288);
        p = new(float.Clamp(p.X / 15, -3,3), float.Clamp(p.Y / 15, -3,3));


        _svg.Render(g,
            (element, fired) =>
            {
                if (element.GetAttribute("id").Equals("RightPuppil") || element.GetAttribute("id").Equals("LeftPuppil"))
                {

                    g.SetTransform(new Vector2(150,150) + p * new Vector2(1,1.5f), Angle.Zero, Vector2.One*2);
                    fired.Fire();
                }
                else if (
                    element.GetAttribute("id").Equals("path9") ||
                    element.GetAttribute("id").Equals("path8") ||
                    element.GetAttribute("id").Equals("path7") ||
                    element.GetAttribute("id").Equals("path6") ||
                    element.GetAttribute("id").Equals("path28") ||
                    element.GetAttribute("id").Equals("path15"))
                {
                    g.SetTransform(new Vector2(150, 150 + p.Y / 2), Angle.Zero, Vector2.One*2);
                }
                else if (
                    element.GetAttribute("id").Equals("LeftEar") ||
                    element.GetAttribute("id").Equals("path27"))
                {
                    g.SetTransform(new Vector2(150, 150 - p.Y / 2), Angle.Zero, Vector2.One*2);
                    fired.Fire();
                }
                else if (
                    element.GetAttribute("id").Equals("g26") ||
                    element.GetAttribute("id").Equals("g27"))
                {
                    g.SetTransform(new Vector2(150, 150 + p.Y / 2), Angle.Zero, Vector2.One*2);
                    fired.Fire();
                }
                else
                {
                    if (!fired.IsFired)
                        g.SetTransform(new Vector2(150,150), Angle.Zero, Vector2.One*2);
                }
            });
    }
}
