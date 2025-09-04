using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct Edge(int index, Vector2 p, Vector2 q)
{
    public Vector2 P { get; set; } = p;
    public Vector2 Q { get; set; } = q;
    public int Index { get; set; } = index;
}
