using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct Triangle(int t, IEnumerable<Vector2> points)
{
    public int Index { get; set; } = t;

    public IEnumerable<Vector2> Points { get; set; } = points;
}
