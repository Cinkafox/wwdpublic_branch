using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct Triangle(int t, IEnumerable<Vector2d> points)
{
    public int Index { get; set; } = t;

    public IEnumerable<Vector2d> Points { get; set; } = points;
}
