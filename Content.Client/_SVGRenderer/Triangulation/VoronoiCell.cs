using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct VoronoiCell(int triangleIndex, Vector2d[] points)
{
    public Vector2d[] Points { get; set; } = points;
    public int Index { get; set; } = triangleIndex;
}
