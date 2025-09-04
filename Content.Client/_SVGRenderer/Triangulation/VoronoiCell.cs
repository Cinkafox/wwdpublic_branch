using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public struct VoronoiCell(int triangleIndex, Vector2[] points)
{
    public Vector2[] Points { get; set; } = points;
    public int Index { get; set; } = triangleIndex;
}
