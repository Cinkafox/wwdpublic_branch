using Robust.Client.Graphics;


namespace Content.Client._SVGRenderer;


public sealed class SvgSystem: EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    public override void Initialize()
    {
        _overlayManager.AddOverlay(new SvgRenderOverlay());
    }
}
