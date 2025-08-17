using System.Linq;
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
            (element, matrix) =>
            {
                SvgTransformHelper.Define(element, matrix)
                    .WithRoot()
                    .Transform(new(50,50))
                    .Scale(new(4));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("RightPuppil", "LeftPuppil")
                    .Transform(p * new Vector2(1, 1.5f));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("path9", "path8", "path7", "path6", "path28", "Mouth")
                    .Transform(new Vector2(0, p.Y / 2));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("LeftEar", "path27")
                    .Transform(new Vector2(0,- p.Y / 2));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("g26", "g27")
                    .Transform(new Vector2(0, p.Y / 2));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("LeftHand")
                    .Rotate(Angle.FromDegrees(double.Sin(_gameTiming.CurFrame/4d)*15),Vector2.Transform(new Vector2(124, 100), matrix.Matrix));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("layer3")
                    .Rotate(Angle.FromDegrees(-double.Sin(_gameTiming.CurFrame/4d)*15),Vector2.Transform(new Vector2(105, 102), matrix.Matrix));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("RightLeg")
                    .Rotate(Angle.FromDegrees(double.Sin(_gameTiming.CurFrame/4d+double.Pi/2d)*15),Vector2.Transform(new Vector2(99, 147), matrix.Matrix));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("LeftLeg")
                    .Rotate(Angle.FromDegrees(double.Sin(_gameTiming.CurFrame/4d)*15),Vector2.Transform(new Vector2(117, 147), matrix.Matrix));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("g2")
                    .Rotate(Angle.FromDegrees(double.Sin(_gameTiming.CurFrame/4d+double.Pi/2d)*15+15),Vector2.Transform(new Vector2(100, 162), matrix.Matrix));

                SvgTransformHelper.Define(element, matrix)
                    .WithIds("g3")
                    .Rotate(Angle.FromDegrees(double.Sin(_gameTiming.CurFrame/4d)*15+15),Vector2.Transform(new Vector2(117, 162), matrix.Matrix));
            });
    }
}

public sealed class SvgTransformHelper
{
    private MiniXmlElement _element;
    private MatrixHandler _matrix;

    private string[] _ids = [];
    private bool _checkRoot;

    public SvgTransformHelper(MiniXmlElement element, MatrixHandler matrix)
    {
        _element = element;
        _matrix = matrix;
    }

    public static SvgTransformHelper Define(MiniXmlElement element, MatrixHandler matrix) =>
        new SvgTransformHelper(element, matrix);

    public SvgTransformHelper WithIds(params string[] ids)
    {
        _ids = ids;
        return this;
    }

    public SvgTransformHelper WithRoot()
    {
        _checkRoot = true;
        return this;
    }

    public SvgTransformHelper Append(in Matrix3x2 matrix)
    {
        if(CheckDoExecute())
            _matrix.Append(matrix);

        return this;
    }

    public SvgTransformHelper Transform(in Vector2 pos) =>
        Append(Matrix3x2.CreateTranslation(pos));

    public SvgTransformHelper Rotate(in Angle angle) =>
        Append(Matrix3x2.CreateRotation((float)angle));

    public SvgTransformHelper Rotate(in Angle angle, in Vector2 center) =>
        Append(Matrix3x2.CreateRotation((float)angle, center));

    public SvgTransformHelper Scale(in Vector2 scale) =>
        Append(Matrix3x2.CreateScale(scale));

    private bool CheckDoExecute()
    {
        if (_checkRoot && _element.Parent is null)
            return true;

        return _ids.Contains(_element.GetAttribute("id"));
    }
}
