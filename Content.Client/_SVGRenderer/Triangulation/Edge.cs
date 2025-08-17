using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct Edge(int index, Vector2d p, Vector2d q)
{
    public Vector2d P { get; set; } = p;
    public Vector2d Q { get; set; } = q;
    public int Index { get; set; } = index;
}
