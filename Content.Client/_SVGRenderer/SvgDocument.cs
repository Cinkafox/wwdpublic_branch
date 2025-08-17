using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Robust.Client.Graphics;


namespace Content.Client._SVGRenderer;

public sealed class SvgDocument
{
    public float Width = 0;
    public float Height = 0;
    public MiniXmlElement Xml = default!;
    public Box2? ViewBox;

    private static readonly Regex WhiteSep = new Regex(@"[\s,]+");

    public static SvgDocument Load(string xmlContent)
    {
        var root = MiniXmlParser.Parse(xmlContent);
        var svg = new SvgDocument { Xml = root };

        if (root == null || root.Name != "svg") throw new Exception("Not an SVG root. ");

        if (root.HasAttribute("width")) float.TryParse(root.GetAttribute("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out svg.Width);
        if (root.HasAttribute("height")) float.TryParse(root.GetAttribute("height"), NumberStyles.Float, CultureInfo.InvariantCulture, out svg.Height);

        if (root.HasAttribute("viewBox"))
        {
            var vb = WhiteSep.Split(root.GetAttribute("viewBox").Trim());
            if (vb.Length >= 4 &&
                float.TryParse(vb[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(vb[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                float.TryParse(vb[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
                float.TryParse(vb[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            {
                svg.ViewBox = Box2.FromDimensions(x,y,w,h);
            }
        }

        return svg;
    }

    public void Render(DrawingHandleBase g, Action<MiniXmlElement, MatrixHandler>? beforeDraw = null)
    {
        var handler = new MatrixHandler();

        beforeDraw?.Invoke(Xml, handler);

        foreach (var node in Xml.Children)
        {
            RenderElement(g, node, new SvgStyle(), handler.Clone(), beforeDraw);
        }
    }

    private void RenderElement(DrawingHandleBase g, MiniXmlElement node, SvgStyle parentStyle, MatrixHandler matrixHandler , Action<MiniXmlElement, MatrixHandler>? beforeDraw = null)
    {
        DefineTransform(node, matrixHandler);

        beforeDraw?.Invoke(node, matrixHandler);

        if(!node.TryGetNonSerializedAttribute<SvgStyle>("style", out var style))
        {
            style = SvgStyle.Combine(parentStyle, SvgStyle.FromElement(node));
            node.NonSerializedAttributes.Add("style", style);
        }

        g.SetTransform(matrixHandler.Matrix);

        switch (node.Name)
        {
            case "g":
                foreach (var child in node.Children)
                    RenderElement(g, child, style, matrixHandler.Clone(), beforeDraw);
                break;
            case "rect":
                DrawRect(g, node, style);
                break;
            case "circle":
                DrawCircle(g, node, style);
                break;
            case "ellipse":
                DrawEllipse(g, node, style);
                break;
            case "line":
                DrawLine(g, node, style);
                break;
            case "polyline":
                DrawPoly(g, node, style, close: false);
                break;
            case "polygon":
                DrawPoly(g, node, style, close: true);
                break;
            case "path":
                DrawPath(g, node, style);
                break;
            default:
                //Logger.Error("Unknown element: " + el.Name);
                break;
        }
    }

    private void DefineTransform(MiniXmlElement node, MatrixHandler matrixHandler)
    {
        if (!node.HasAttribute("transform"))
            return;

        if(!node.TryGetNonSerializedAttribute<List<SvgTransformParser.TransformCmd>>("transform", out var transform))
        {
            transform = SvgTransformParser.ParseTransform(node.GetAttribute("transform"));
            node.NonSerializedAttributes.Add("transform", transform);
        }

        foreach (var cmd in transform)
        {
            switch (cmd.Type)
            {
                case SvgTransformParser.TransformType.Translate:
                    if (cmd.Params.Length == 1)
                        matrixHandler.Transform(new Vector2(cmd.Params[0], 0));
                    else if (cmd.Params.Length >= 2)
                        matrixHandler.Transform(new Vector2(cmd.Params[0], cmd.Params[1]));
                    break;

                case SvgTransformParser.TransformType.Rotate:
                    if (cmd.Params.Length == 1)
                    {
                        matrixHandler.Rotate(Angle.FromDegrees(cmd.Params[0]));
                    }
                    else if (cmd.Params.Length >= 3)
                    {
                        matrixHandler.Rotate(Angle.FromDegrees(cmd.Params[0]),
                            new Vector2(cmd.Params[1], cmd.Params[2]));
                    }
                    break;

                case SvgTransformParser.TransformType.Scale:
                    if (cmd.Params.Length == 1)
                        matrixHandler.Scale(new Vector2(cmd.Params[0], cmd.Params[0]));
                    else if (cmd.Params.Length >= 2)
                        matrixHandler.Scale(new Vector2(cmd.Params[0], cmd.Params[1]));
                    break;

                case SvgTransformParser.TransformType.SkewX:
                    if (cmd.Params.Length >= 1)
                    {
                        var angle = Angle.FromDegrees(cmd.Params[0]);
                        matrixHandler.Append(new Matrix3x2(1, 0, (float)Math.Tan(angle), 1, 0, 0));
                    }
                    break;

                case SvgTransformParser.TransformType.SkewY:
                    if (cmd.Params.Length >= 1)
                    {
                        var angle = Angle.FromDegrees(cmd.Params[0]);
                        matrixHandler.Append(new Matrix3x2(1, (float)Math.Tan(angle), 0, 1, 0, 0));
                    }
                    break;

                case SvgTransformParser.TransformType.Matrix:
                    if (cmd.Params.Length == 6)
                    {
                        matrixHandler.Append(new Matrix3x2(
                            cmd.Params[0], cmd.Params[1],
                            cmd.Params[2], cmd.Params[3],
                            cmd.Params[4], cmd.Params[5]
                        ));
                    }
                    break;
            }
        }
    }

    private void DrawRect(DrawingHandleBase g, MiniXmlElement el, SvgStyle style)
    {
        var x = float.Parse(el.GetAttribute("x") ?? "0", CultureInfo.InvariantCulture);
        var y = float.Parse(el.GetAttribute("y") ?? "0", CultureInfo.InvariantCulture);
        var w = float.Parse(el.GetAttribute("width") ?? "0", CultureInfo.InvariantCulture);
        var h = float.Parse(el.GetAttribute("height") ?? "0", CultureInfo.InvariantCulture);

        var vertices = new List<Vector2>
        {
            new Vector2(x, y),
            new Vector2(x + w, y),
            new Vector2(x + w, y + h),
            new Vector2(x, y + h),
        };

        if (style.Fill.HasValue)
        {
            // Triangle fan for filled rectangle
            var filledVerts = new List<Vector2>
            {
                vertices[0], vertices[1], vertices[2], vertices[3]
            };
            g.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, filledVerts, style.Fill.Value);
        }

        if (style.Stroke.HasValue)
        {
            // Close loop for outline
            var outlineVerts = new List<Vector2>(vertices) { vertices[0] };
            g.DrawPrimitives(DrawPrimitiveTopology.LineStrip, outlineVerts, style.Stroke.Value);
        }
    }

    private void DrawCircle(DrawingHandleBase g, MiniXmlElement el, SvgStyle style)
    {
        var cx = float.Parse(el.GetAttribute("cx") ?? "0", CultureInfo.InvariantCulture);
        var cy = float.Parse(el.GetAttribute("cy") ?? "0", CultureInfo.InvariantCulture);
        var r = float.Parse(el.GetAttribute("r") ?? "0", CultureInfo.InvariantCulture);

        const int segments = 64;
        var center = new Vector2(cx, cy);

        if (style.Fill.HasValue)
        {
            var verts = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * MathHelper.TwoPi;
                verts.Add(center + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * r);
            }
            g.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, style.Fill.Value);
        }

        if (style.Stroke.HasValue)
        {
            var verts = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * MathHelper.TwoPi;
                verts.Add(center + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * r);
            }
            g.DrawPrimitives(DrawPrimitiveTopology.LineStrip, verts, style.Stroke.Value);
        }
    }

    private void DrawEllipse(DrawingHandleBase g, MiniXmlElement el, SvgStyle style)
    {
        var cx = float.Parse(el.GetAttribute("cx") ?? "0", CultureInfo.InvariantCulture);
        var cy = float.Parse(el.GetAttribute("cy") ?? "0", CultureInfo.InvariantCulture);
        var rx = float.Parse(el.GetAttribute("rx") ?? "0", CultureInfo.InvariantCulture);
        var ry = float.Parse(el.GetAttribute("ry") ?? "0", CultureInfo.InvariantCulture);

        const int segments = 64;
        var center = new Vector2(cx, cy);

        if (style.Fill.HasValue)
        {
            var verts = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * MathHelper.TwoPi;
                verts.Add(center + new Vector2(MathF.Sin(angle) * rx, MathF.Cos(angle) * ry));
            }
            g.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, style.Fill.Value);
        }

        if (style.Stroke.HasValue)
        {
            var verts = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * MathHelper.TwoPi;
                verts.Add(center + new Vector2(MathF.Sin(angle) * rx, MathF.Cos(angle) * ry));
            }
            g.DrawPrimitives(DrawPrimitiveTopology.LineStrip, verts, style.Stroke.Value);
        }
    }

    private void DrawLine(DrawingHandleBase g, MiniXmlElement el, SvgStyle style)
    {
        var x1 = float.Parse(el.GetAttribute("x1") ?? "0", CultureInfo.InvariantCulture);
        var y1 = float.Parse(el.GetAttribute("y1") ?? "0", CultureInfo.InvariantCulture);
        var x2 = float.Parse(el.GetAttribute("x2") ?? "0", CultureInfo.InvariantCulture);
        var y2 = float.Parse(el.GetAttribute("y2") ?? "0", CultureInfo.InvariantCulture);

        if (style.Stroke.HasValue)
        {
            g.DrawPrimitives(
                DrawPrimitiveTopology.LineList,
                new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y2) },
                style.Stroke.Value
            );
        }
    }

    private void DrawPoly(DrawingHandleBase g, MiniXmlElement el, SvgStyle style, bool close)
    {
        var pointsStr = el.GetAttribute("points") ?? "";
        var points = pointsStr
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select((v, i) => new { v, i })
            .GroupBy(x => x.i / 2)
            .Select(grp => new Vector2(float.Parse(grp.ElementAt(0).v, CultureInfo.InvariantCulture), float.Parse(grp.ElementAt(1).v, CultureInfo.InvariantCulture)))
            .ToList();

        if (style.Fill.HasValue && close)
        {
            g.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, style.Fill.Value);
        }

        if (style.Stroke.HasValue)
        {
            var outline = new List<Vector2>(points);
            if (close) outline.Add(points[0]);
            g.DrawPrimitives(DrawPrimitiveTopology.LineStrip, outline, style.Stroke.Value);
        }
    }

    private List<Vector2> vertices = new();

    private void DrawPath(DrawingHandleBase g, MiniXmlElement el, SvgStyle style)
    {
        var d = el.GetAttribute("d");
        if (string.IsNullOrWhiteSpace(d))
            return;

        if (!el.TryGetNonSerializedAttribute<List<SvgPathParser.SvgCmd>>("d", out var path))
        {
            path = SvgPathParser.ParsePath(d);
            el.NonSerializedAttributes["d"] = path;
        }

        vertices.Clear();

        float currentX = 0, currentY = 0;
        float lastCtrlX = 0, lastCtrlY = 0;
        float startX = 0, startY = 0;

        foreach (var cmd in path)
        {
            var c = cmd.Cmd;
            var p = cmd.Params;

            switch (c)
            {
                case 'M':
                case 'm':
                    currentX = (c == 'm') ? currentX + p[0] : p[0];
                    currentY = (c == 'm') ? currentY + p[1] : p[1];
                    startX = currentX; startY = currentY;
                    vertices.Add(new Vector2(currentX, currentY));
                    break;

                case 'L':
                case 'l':
                    {
                        var x = (c == 'l') ? currentX + p[0] : p[0];
                        var y = (c == 'l') ? currentY + p[1] : p[1];
                        vertices.Add(new Vector2(x, y));
                        currentX = x; currentY = y;
                    }
                    break;

                case 'H':
                case 'h':
                    {
                        var x = (c == 'h') ? currentX + p[0] : p[0];
                        vertices.Add(new Vector2(x, currentY));
                        currentX = x;
                    }
                    break;

                case 'V':
                case 'v':
                    {
                        var y = (c == 'v') ? currentY + p[0] : p[0];
                        vertices.Add(new Vector2(currentX, y));
                        currentY = y;
                    }
                    break;

                case 'C':
                case 'c':
                    {
                        var x1 = (c == 'c') ? currentX + p[0] : p[0];
                        var y1 = (c == 'c') ? currentY + p[1] : p[1];
                        var x2 = (c == 'c') ? currentX + p[2] : p[2];
                        var y2 = (c == 'c') ? currentY + p[3] : p[3];
                        var x = (c == 'c') ? currentX + p[4] : p[4];
                        var y = (c == 'c') ? currentY + p[5] : p[5];

                        foreach (var pos in ApproximateCubicBezier( currentX, currentY, x1, y1, x2, y2, x, y))
                        {
                            vertices.Add(pos);
                        }

                        lastCtrlX = x2; lastCtrlY = y2;
                        currentX = x; currentY = y;
                    }
                    break;

                case 'S':
                case 's':
                    {
                        var x1 = currentX * 2 - lastCtrlX;
                        var y1 = currentY * 2 - lastCtrlY;
                        var x2 = (c == 's') ? currentX + p[0] : p[0];
                        var y2 = (c == 's') ? currentY + p[1] : p[1];
                        var x = (c == 's') ? currentX + p[2] : p[2];
                        var y = (c == 's') ? currentY + p[3] : p[3];

                        foreach (var pos in ApproximateCubicBezier(currentX, currentY, x1, y1, x2, y2, x, y))
                        {
                            vertices.Add(pos);
                        }

                        lastCtrlX = x2; lastCtrlY = y2;
                        currentX = x; currentY = y;
                    }
                    break;

                case 'Q':
                case 'q':
                    {
                        var x1 = (c == 'q') ? currentX + p[0] : p[0];
                        var y1 = (c == 'q') ? currentY + p[1] : p[1];
                        var x = (c == 'q') ? currentX + p[2] : p[2];
                        var y = (c == 'q') ? currentY + p[3] : p[3];

                        // Convert quadratic to cubic
                        var cx1 = currentX + 2f / 3 * (x1 - currentX);
                        var cy1 = currentY + 2f / 3 * (y1 - currentY);
                        var cx2 = x + 2f / 3 * (x1 - x);
                        var cy2 = y + 2f / 3 * (y1 - y);

                        foreach (var pos in ApproximateCubicBezier( currentX, currentY, cx1, cy1, cx2, cy2, x, y))
                        {
                            vertices.Add(pos);
                        }

                        lastCtrlX = x1; lastCtrlY = y1;
                        currentX = x; currentY = y;
                    }
                    break;

                case 'T':
                case 't':
                    {
                        var x1 = currentX * 2 - lastCtrlX;
                        var y1 = currentY * 2 - lastCtrlY;
                        var x = (c == 't') ? currentX + p[0] : p[0];
                        var y = (c == 't') ? currentY + p[1] : p[1];

                        var cx1 = currentX + 2f / 3 * (x1 - currentX);
                        var cy1 = currentY + 2f / 3 * (y1 - currentY);
                        var cx2 = x + 2f / 3 * (x1 - x);
                        var cy2 = y + 2f / 3 * (y1 - y);

                        foreach (var pos in ApproximateCubicBezier( currentX, currentY, cx1, cy1, cx2, cy2, x, y))
                        {
                            vertices.Add(pos);
                        }

                        lastCtrlX = x1; lastCtrlY = y1;
                        currentX = x; currentY = y;
                    }
                    break;

                case 'Z':
                case 'z':
                    vertices.Add(new Vector2(startX, startY));
                    currentX = startX; currentY = startY;
                    break;

                default:
                    Logger.Error($"[DrawPath] Unsupported command: {c}");
                    break;
            }
        }

        if (style.Fill.HasValue && vertices.Count >= 3)
        {
            if(EarClipping.Triangulate(vertices, out var tris))
                g.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tris, style.Fill.Value);
            else
                g.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, style.Fill.Value);
        }
        if (style.Stroke.HasValue && vertices.Count >= 2)
        {
            g.DrawPrimitives(DrawPrimitiveTopology.LineStrip, vertices, style.Stroke.Value);
        }
    }

    private IEnumerable<Vector2> ApproximateCubicBezier(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, int segments = 16)
    {
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1 - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;

            var x = uuu * x0 + 3 * uu * t * x1 + 3 * u * tt * x2 + ttt * x3;
            var y = uuu * y0 + 3 * uu * t * y1 + 3 * u * tt * y2 + ttt * y3;

            yield return new(x, y);
        }
    }
}

public static class EarClipping
{
    public static bool Triangulate(List<Vector2> polygon, out List<Vector2> triangles)
    {
        triangles = new List<Vector2>();
        var vertices = new List<Vector2>(polygon);

        if (vertices.Count < 3)
            return false;

        while (vertices.Count > 3)
        {
            var earFound = false;
            for (var i = 0; i < vertices.Count; i++)
            {
                var prev = vertices[(i - 1 + vertices.Count) % vertices.Count];
                var curr = vertices[i];
                var next = vertices[(i + 1) % vertices.Count];

                if (IsConvex(prev, curr, next))
                {
                    // Check if any other vertex lies inside this potential ear triangle
                    var hasPointInside = vertices.Any(p => p != prev && p != curr && p != next && PointInTriangle(p, prev, curr, next));

                    if (!hasPointInside)
                    {
                        triangles.Add(prev);
                        triangles.Add(curr);
                        triangles.Add(next);

                        vertices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }
            }

            if (!earFound)
            {
                return false;
            }
        }

        triangles.Add(vertices[0]);
        triangles.Add(vertices[1]);
        triangles.Add(vertices[2]);

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(a, b, c) > 0; // assuming counter-clockwise polygon
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var cross1 = Cross(a, b, p);
        var cross2 = Cross(b, c, p);
        var cross3 = Cross(c, a, p);
        var hasNeg = (cross1 < 0) || (cross2 < 0) || (cross3 < 0);
        var hasPos = (cross1 > 0) || (cross2 > 0) || (cross3 > 0);
        return !(hasNeg && hasPos);
    }
}


public sealed class MatrixHandler
{
    public Matrix3x2 Matrix {get; private set;} = Matrix3x2.Identity;

    public void Append(in Matrix3x2 value) =>
        Matrix *= value;

    public void Transform(in Vector2 pos) =>
        Append(Matrix3x2.CreateTranslation(pos));

    public void Rotate(in Angle angle) =>
        Append(Matrix3x2.CreateRotation((float)angle));

    public void Rotate(in Angle angle,in Vector2 center) =>
        Append(Matrix3x2.CreateRotation((float)angle, center));

    public void Scale(in Vector2 scale) =>
        Append(Matrix3x2.CreateScale(scale));

    public MatrixHandler Clone() =>
        new()
        {
            Matrix = Matrix
        };
}
