using System.Linq;
using System.Numerics;


namespace Content.Client._SVGRenderer.Triangulation;


public sealed class Delaunator
{
    private readonly float[] _coords;
    private readonly int[] _edgeStack = new int[512];
    private readonly float _epsilon = float.Pow(2, -52);

    private readonly int _hashSize;
    private readonly int[] _hullPrev;
    private readonly int _hullStart;
    private readonly int[] _hullTri;

    private readonly float _cx;
    private readonly float _cy;

    private int _trianglesLen;

    public Delaunator(Vector2[] vector2s)
    {
        if (vector2s.Length < 3)
            throw new ArgumentOutOfRangeException("Need at least 3 Vector2s");

        Vector2s = vector2s;
        _coords = new float[Vector2s.Length * 2];

        for (var i = 0; i < Vector2s.Length; i++)
        {
            var p = Vector2s[i];
            _coords[2 * i] = p.X;
            _coords[2 * i + 1] = p.Y;
        }

        var n = Vector2s.Length;
        var maxTriangles = 2 * n - 5;

        Triangles = new int[maxTriangles * 3];

        Halfedges = new int[maxTriangles * 3];
        _hashSize = (int) Math.Ceiling(Math.Sqrt(n));

        _hullPrev = new int[n];
        var hullNext = new int[n];
        _hullTri = new int[n];
        var hullHash = new int[_hashSize];

        var ids = new int[n];

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        for (var i = 0; i < n; i++)
        {
            var x = _coords[2 * i];
            var y = _coords[2 * i + 1];
            if (x < minX)
                minX = x;
            if (y < minY)
                minY = y;
            if (x > maxX)
                maxX = x;
            if (y > maxY)
                maxY = y;
            ids[i] = i;
        }

        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;

        var minDist = float.PositiveInfinity;
        var i0 = 0;
        var i1 = 0;
        var i2 = 0;

        // pick a seed Vector2 close to the center
        for (var i = 0; i < n; i++)
        {
            var d = Dist(cx, cy, _coords[2 * i], _coords[2 * i + 1]);
            if (d < minDist)
            {
                i0 = i;
                minDist = d;
            }
        }

        var i0X = _coords[2 * i0];
        var i0Y = _coords[2 * i0 + 1];

        minDist = float.PositiveInfinity;

        // find the Vector2 closest to the seed
        for (var i = 0; i < n; i++)
        {
            if (i == i0)
                continue;
            var d = Dist(i0X, i0Y, _coords[2 * i], _coords[2 * i + 1]);
            if (d < minDist && d > 0)
            {
                i1 = i;
                minDist = d;
            }
        }

        var i1X = _coords[2 * i1];
        var i1Y = _coords[2 * i1 + 1];

        var minRadius = float.PositiveInfinity;

        // find the third Vector2 which forms the smallest circumcircle with the first two
        for (var i = 0; i < n; i++)
        {
            if (i == i0 || i == i1)
                continue;
            var r = Circumradius(i0X, i0Y, i1X, i1Y, _coords[2 * i], _coords[2 * i + 1]);
            if (r < minRadius)
            {
                i2 = i;
                minRadius = r;
            }
        }

        var i2X = _coords[2 * i2];
        var i2Y = _coords[2 * i2 + 1];

        if (float.IsPositiveInfinity(minRadius))
            throw new("No Delaunay triangulation exists for this input.");

        if (Orient(i0X, i0Y, i1X, i1Y, i2X, i2Y))
        {
            var i = i1;
            var x = i1X;
            var y = i1Y;
            i1 = i2;
            i1X = i2X;
            i1Y = i2Y;
            i2 = i;
            i2X = x;
            i2Y = y;
        }

        var center = Circumcenter(i0X, i0Y, i1X, i1Y, i2X, i2Y);
        this._cx = center.X;
        this._cy = center.Y;

        var dists = new float[n];
        for (var i = 0; i < n; i++)
            dists[i] = Dist(_coords[2 * i], _coords[2 * i + 1], center.X, center.Y);

        // sort the Vector2s by distance from the seed triangle circumcenter
        Quicksort(ids, dists, 0, n - 1);

        // set up the seed triangle as the starting hull
        _hullStart = i0;
        var hullSize = 3;

        hullNext[i0] = _hullPrev[i2] = i1;
        hullNext[i1] = _hullPrev[i0] = i2;
        hullNext[i2] = _hullPrev[i1] = i0;

        _hullTri[i0] = 0;
        _hullTri[i1] = 1;
        _hullTri[i2] = 2;

        hullHash[HashKey(i0X, i0Y)] = i0;
        hullHash[HashKey(i1X, i1Y)] = i1;
        hullHash[HashKey(i2X, i2Y)] = i2;

        _trianglesLen = 0;
        AddTriangle(i0, i1, i2, -1, -1, -1);

        float xp = 0;
        float yp = 0;

        for (var k = 0; k < ids.Length; k++)
        {
            var i = ids[k];
            var x = _coords[2 * i];
            var y = _coords[2 * i + 1];

            // skip near-duplicate Vector2s
            if (k > 0 && Math.Abs(x - xp) <= _epsilon && Math.Abs(y - yp) <= _epsilon)
                continue;
            xp = x;
            yp = y;

            // skip seed triangle Vector2s
            if (i == i0 || i == i1 || i == i2)
                continue;

            // find a visible edge on the convex hull using edge hash
            var start = 0;
            for (var j = 0; j < _hashSize; j++)
            {
                var key = HashKey(x, y);
                start = hullHash[(key + j) % _hashSize];
                if (start != -1 && start != hullNext[start])
                    break;
            }


            start = _hullPrev[start];
            var e = start;
            var q = hullNext[e];

            while (!Orient(x, y, _coords[2 * e], _coords[2 * e + 1], _coords[2 * q], _coords[2 * q + 1]))
            {
                e = q;
                if (e == start)
                {
                    e = int.MaxValue;
                    break;
                }

                q = hullNext[e];
            }

            if (e == int.MaxValue)
                continue; // likely a near-duplicate Vector2; skip it

            // add the first triangle from the Vector2
            var t = AddTriangle(e, i, hullNext[e], -1, -1, _hullTri[e]);

            // recursively flip triangles from the Vector2 until they satisfy the Delaunay condition
            _hullTri[i] = Legalize(t + 2);
            _hullTri[e] = t; // keep track of boundary triangles on the hull
            hullSize++;

            // walk forward through the hull, adding more triangles and flipping recursively
            var next = hullNext[e];
            q = hullNext[next];

            while (Orient(x, y, _coords[2 * next], _coords[2 * next + 1], _coords[2 * q], _coords[2 * q + 1]))
            {
                t = AddTriangle(next, i, q, _hullTri[i], -1, _hullTri[next]);
                _hullTri[i] = Legalize(t + 2);
                hullNext[next] = next; // mark as removed
                hullSize--;
                next = q;

                q = hullNext[next];
            }

            // walk backward from the other side, adding more triangles and flipping
            if (e == start)
            {
                q = _hullPrev[e];

                while (Orient(x, y, _coords[2 * q], _coords[2 * q + 1], _coords[2 * e], _coords[2 * e + 1]))
                {
                    t = AddTriangle(q, i, e, -1, _hullTri[e], _hullTri[q]);
                    Legalize(t + 2);
                    _hullTri[q] = t;
                    hullNext[e] = e; // mark as removed
                    hullSize--;
                    e = q;

                    q = _hullPrev[e];
                }
            }

            // update the hull indices
            _hullStart = _hullPrev[i] = e;
            hullNext[e] = _hullPrev[next] = i;
            hullNext[i] = next;

            // save the two new edges in the hash table
            hullHash[HashKey(x, y)] = i;
            hullHash[HashKey(_coords[2 * e], _coords[2 * e + 1])] = e;
        }

        Hull = new int[hullSize];
        var s = _hullStart;
        for (var i = 0; i < hullSize; i++)
        {
            Hull[i] = s;
            s = hullNext[s];
        }

        _hullPrev = hullNext = _hullTri = default!; // get rid of temporary arrays

        //// trim typed triangle mesh arrays
        Triangles = Triangles.Take(_trianglesLen).ToArray();
        Halfedges = Halfedges.Take(_trianglesLen).ToArray();
    }

    /// <summary>
    ///     One value per half-edge, containing the Vector2 index of where a given half edge starts.
    /// </summary>
    public int[] Triangles { get; }

    /// <summary>
    ///     One value per half-edge, containing the opposite half-edge in the adjacent triangle, or -1 if there is no adjacent
    ///     triangle
    /// </summary>
    public int[] Halfedges { get; }

    /// <summary>
    ///     The initial Vector2s Delaunator was constructed with.
    /// </summary>
    public Vector2[] Vector2s { get; } = [];

    /// <summary>
    ///     A list of Vector2 indices that traverses the hull of the Vector2s.
    /// </summary>
    public int[] Hull { get; }

    #region CreationLogic

    private int Legalize(int a)
    {
        var i = 0;
        int ar;

        // recursion eliminated with a fixed-size stack
        while (true)
        {
            var b = Halfedges[a];

            /* if the pair of triangles doesn't satisfy the Delaunay condition
             * (p1 is inside the circumcircle of [p0, pl, pr]), flip them,
             * then do the same check/flip recursively for the new pair of triangles
             *
             *           pl                    pl
             *          /||\                  /  \
             *       al/ || \bl            al/    \a
             *        /  ||  \              /      \
             *       /  a||b  \    flip    /___ar___\
             *     p0\   ||   /p1   =>   p0\---bl---/p1
             *        \  ||  /              \      /
             *       ar\ || /br             b\    /br
             *          \||/                  \  /
             *           pr                    pr
             */
            var a0 = a - a % 3;
            ar = a0 + (a + 2) % 3;

            if (b == -1)
            {
                // convex hull edge
                if (i == 0)
                    break;
                a = _edgeStack[--i];
                continue;
            }

            var b0 = b - b % 3;
            var al = a0 + (a + 1) % 3;
            var bl = b0 + (b + 2) % 3;

            var p0 = Triangles[ar];
            var pr = Triangles[a];
            var pl = Triangles[al];
            var p1 = Triangles[bl];

            var illegal = InCircle(
                _coords[2 * p0],
                _coords[2 * p0 + 1],
                _coords[2 * pr],
                _coords[2 * pr + 1],
                _coords[2 * pl],
                _coords[2 * pl + 1],
                _coords[2 * p1],
                _coords[2 * p1 + 1]);

            if (illegal)
            {
                Triangles[a] = p1;
                Triangles[b] = p0;

                var hbl = Halfedges[bl];

                // edge swapped on the other side of the hull (rare); fix the halfedge reference
                if (hbl == -1)
                {
                    var e = _hullStart;
                    do
                    {
                        if (_hullTri[e] == bl)
                        {
                            _hullTri[e] = a;
                            break;
                        }

                        e = _hullPrev[e];
                    } while (e != _hullStart);
                }

                Link(a, hbl);
                Link(b, Halfedges[ar]);
                Link(ar, bl);

                var br = b0 + (b + 1) % 3;

                // don't worry about hitting the cap: it can only happen on extremely degenerate input
                if (i < _edgeStack.Length)
                    _edgeStack[i++] = br;
            }
            else
            {
                if (i == 0)
                    break;
                a = _edgeStack[--i];
            }
        }

        return ar;
    }

    private static bool InCircle(float ax, float ay, float bx, float by, float cx, float cy, float px, float py)
    {
        var dx = ax - px;
        var dy = ay - py;
        var ex = bx - px;
        var ey = by - py;
        var fx = cx - px;
        var fy = cy - py;

        var ap = dx * dx + dy * dy;
        var bp = ex * ex + ey * ey;
        var cp = fx * fx + fy * fy;

        return dx * (ey * cp - bp * fy) -
            dy * (ex * cp - bp * fx) +
            ap * (ex * fy - ey * fx) < 0;
    }

    private int AddTriangle(int i0, int i1, int i2, int a, int b, int c)
    {
        var t = _trianglesLen;

        Triangles[t] = i0;
        Triangles[t + 1] = i1;
        Triangles[t + 2] = i2;

        Link(t, a);
        Link(t + 1, b);
        Link(t + 2, c);

        _trianglesLen += 3;
        return t;
    }

    private void Link(int a, int b)
    {
        Halfedges[a] = b;
        if (b != -1)
            Halfedges[b] = a;
    }

    private int HashKey(float x, float y) => (int) (Math.Floor(PseudoAngle(x - _cx, y - _cy) * _hashSize) % _hashSize);

    private static float PseudoAngle(float dx, float dy)
    {
        var p = dx / (Math.Abs(dx) + Math.Abs(dy));
        return (dy > 0 ? 3 - p : 1 + p) / 4; // [0..1]
    }

    private static void Quicksort(int[] ids, float[] dists, int left, int right)
    {
        if (right - left <= 20)
        {
            for (var i = left + 1; i <= right; i++)
            {
                var temp = ids[i];
                var tempDist = dists[temp];
                var j = i - 1;
                while (j >= left && dists[ids[j]] > tempDist)
                    ids[j + 1] = ids[j--];
                ids[j + 1] = temp;
            }
        }
        else
        {
            var median = (left + right) >> 1;
            var i = left + 1;
            var j = right;
            Swap(ids, median, i);
            if (dists[ids[left]] > dists[ids[right]])
                Swap(ids, left, right);
            if (dists[ids[i]] > dists[ids[right]])
                Swap(ids, i, right);
            if (dists[ids[left]] > dists[ids[i]])
                Swap(ids, left, i);

            var temp = ids[i];
            var tempDist = dists[temp];
            while (true)
            {
                do
                    i++;
                while (dists[ids[i]] < tempDist);
                do
                    j--;
                while (dists[ids[j]] > tempDist);
                if (j < i)
                    break;
                Swap(ids, i, j);
            }

            ids[left + 1] = ids[j];
            ids[j] = temp;

            if (right - i + 1 >= j - left)
            {
                Quicksort(ids, dists, i, right);
                Quicksort(ids, dists, left, j - 1);
            }
            else
            {
                Quicksort(ids, dists, left, j - 1);
                Quicksort(ids, dists, i, right);
            }
        }
    }

    private static void Swap(int[] arr, int i, int j)
    {
        var tmp = arr[i];
        arr[i] = arr[j];
        arr[j] = tmp;
    }

    private static bool Orient(float px, float py, float qx, float qy, float rx, float ry) =>
        (qy - py) * (rx - qx) - (qx - px) * (ry - qy) < 0;

    private static float Circumradius(float ax, float ay, float bx, float by, float cx, float cy)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var ex = cx - ax;
        var ey = cy - ay;
        var bl = dx * dx + dy * dy;
        var cl = ex * ex + ey * ey;
        var d = 0.5f / (dx * ey - dy * ex);
        var x = (ey * bl - dy * cl) * d;
        var y = (dx * cl - ex * bl) * d;
        return x * x + y * y;
    }

    private static Vector2 Circumcenter(float ax, float ay, float bx, float by, float cx, float cy)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var ex = cx - ax;
        var ey = cy - ay;
        var bl = dx * dx + dy * dy;
        var cl = ex * ex + ey * ey;
        var d = 0.5f / (dx * ey - dy * ex);
        var x = ax + (ey * bl - dy * cl) * d;
        var y = ay + (dx * cl - ex * bl) * d;

        return new(x, y);
    }

    private static float Dist(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }

    #endregion CreationLogic

    #region GetMethods

    public IEnumerable<Triangle> GetTriangles()
    {
        for (var t = 0; t < Triangles.Length / 3; t++)
            yield return new(t, GetTriangleVector2s(t));
    }

    public IEnumerable<Edge> GetEdges()
    {
        for (var e = 0; e < Triangles.Length; e++)
            if (e > Halfedges[e])
            {
                var p = Vector2s[Triangles[e]];
                var q = Vector2s[Triangles[NextHalfedge(e)]];
                yield return new(e, p, q);
            }
    }

    public IEnumerable<Edge> GetVoronoEdges(Func<int, Vector2>? triangleVerticeSelector = null)
    {
        if (triangleVerticeSelector == null)
            triangleVerticeSelector = x => GetCentroid(x);
        for (var e = 0; e < Triangles.Length; e++)
            if (e < Halfedges[e])
            {
                var p = triangleVerticeSelector(TriangleOfEdge(e));
                var q = triangleVerticeSelector(TriangleOfEdge(Halfedges[e]));
                yield return new(e, p, q);
            }
    }

    public IEnumerable<Edge> GetVoronoEdgesBasedOnCircumCenter() => GetVoronoEdges(GetTriangleCircumcenter);
    public IEnumerable<Edge> GetVoronoEdgesBasedOnCentroids() => GetVoronoEdges(GetCentroid);

    public IEnumerable<VoronoiCell> GetVoronoiCells(Func<int, Vector2>? triangleVerticeSelector = null)
    {
        if (triangleVerticeSelector == null)
            triangleVerticeSelector = x => GetCentroid(x);

        var seen = new HashSet<int>();
        var vertices = new List<Vector2>(10); // Keep it outside the loop, reuse capacity, less resizes.

        for (var e = 0; e < Triangles.Length; e++)
        {
            var Vector2Index = Triangles[NextHalfedge(e)];
            // True if element was added, If resize the set? O(n) : O(1)
            if (seen.Add(Vector2Index))
            {
                foreach (var edge in EdgesAroundVector2(e))
                    // triangleVerticeSelector cant be null, no need to check before invoke (?.).
                    vertices.Add(triangleVerticeSelector.Invoke(TriangleOfEdge(edge)));
                yield return new(Vector2Index, vertices.ToArray());
                vertices.Clear(); // Clear elements, keep capacity
            }
        }
    }

    public IEnumerable<VoronoiCell> GetVoronoiCellsBasedOnCircumcenters() => GetVoronoiCells(GetTriangleCircumcenter);
    public IEnumerable<VoronoiCell> GetVoronoiCellsBasedOnCentroids() => GetVoronoiCells(GetCentroid);

    public IEnumerable<Edge> GetHullEdges() => CreateHull(GetHullVector2s());

    public Vector2[] GetHullVector2s() => Hull.Select(x => Vector2s[x]).ToArray();

    public Vector2[] GetTriangleVector2s(int t)
    {
        var Vector2s = new List<Vector2>();
        foreach (var p in Vector2sOfTriangle(t))
            Vector2s.Add(Vector2s[p]);
        return Vector2s.ToArray();
    }

    public Vector2[] GetRellaxedVector2s()
    {
        var Vector2s = new List<Vector2>();
        foreach (var cell in GetVoronoiCellsBasedOnCircumcenters())
            Vector2s.Add(GetCentroid(cell.Points));
        return Vector2s.ToArray();
    }

    public IEnumerable<Edge> GetEdgesOfTriangle(int t) =>
        CreateHull(EdgesOfTriangle(t).Select(e => Vector2s[Triangles[e]]));

    public static IEnumerable<Edge> CreateHull(IEnumerable<Vector2> Vector2s) =>
        Vector2s.Zip(Vector2s.Skip(1).Append(Vector2s.FirstOrDefault()), (a, b) => new Edge(0, a, b)).OfType<Edge>();

    public Vector2 GetTriangleCircumcenter(int t)
    {
        var vertices = GetTriangleVector2s(t);
        return GetCircumcenter(vertices[0], vertices[1], vertices[2]);
    }

    public Vector2 GetCentroid(int t)
    {
        var vertices = GetTriangleVector2s(t);
        return GetCentroid(vertices);
    }

    public static Vector2 GetCircumcenter(Vector2 a, Vector2 b, Vector2 c) =>
        Circumcenter(a.X, a.Y, b.X, b.Y, c.X, c.Y);

    public static Vector2 GetCentroid(Vector2[] Vector2s)
    {
        float accumulatedArea = 0.0f;
        float centerX = 0.0f;
        float centerY = 0.0f;

        for (int i = 0, j = Vector2s.Length - 1; i < Vector2s.Length; j = i++)
        {
            var temp = Vector2s[i].X * Vector2s[j].Y - Vector2s[j].X * Vector2s[i].Y;
            accumulatedArea += temp;
            centerX += (Vector2s[i].X + Vector2s[j].X) * temp;
            centerY += (Vector2s[i].Y + Vector2s[j].Y) * temp;
        }

        if (Math.Abs(accumulatedArea) < 1E-7f)
            return new();

        accumulatedArea *= 3f;
        return new(centerX / accumulatedArea, centerY / accumulatedArea);
    }

    #endregion GetMethods

    #region ForEachMethods

    public void ForEachTriangle(Action<Triangle> callback)
    {
        foreach (var triangle in GetTriangles())
            callback?.Invoke(triangle);
    }

    public void ForEachTriangleEdge(Action<Edge> callback)
    {
        foreach (var edge in GetEdges())
            callback?.Invoke(edge);
    }

    public void ForEachVoronoEdge(Action<Edge> callback)
    {
        foreach (var edge in GetVoronoEdges())
            callback?.Invoke(edge);
    }

    public void ForEachVoronoiCellBasedOnCentroids(Action<VoronoiCell> callback)
    {
        foreach (var cell in GetVoronoiCellsBasedOnCentroids())
            callback?.Invoke(cell);
    }

    public void ForEachVoronoiCellBasedOnCircumcenters(Action<VoronoiCell> callback)
    {
        foreach (var cell in GetVoronoiCellsBasedOnCircumcenters())
            callback?.Invoke(cell);
    }

    public void ForEachVoronoiCell(Action<VoronoiCell> callback, Func<int, Vector2>? triangleVertexSelector = null)
    {
        foreach (var cell in GetVoronoiCells(triangleVertexSelector))
            callback?.Invoke(cell);
    }

    #endregion ForEachMethods

    #region Methods based on index

    /// <summary>
    ///     Returns the half-edges that share a start Vector2 with the given half edge, in order.
    /// </summary>
    public IEnumerable<int> EdgesAroundVector2(int start)
    {
        var incoming = start;
        do
        {
            yield return incoming;
            var outgoing = NextHalfedge(incoming);
            incoming = Halfedges[outgoing];
        } while (incoming != -1 && incoming != start);
    }

    /// <summary>
    ///     Returns the three Vector2 indices of a given triangle id.
    /// </summary>
    public IEnumerable<int> Vector2sOfTriangle(int t)
    {
        foreach (var edge in EdgesOfTriangle(t))
            yield return Triangles[edge];
    }

    /// <summary>
    ///     Returns the triangle ids adjacent to the given triangle id.
    ///     Will return up to three values.
    /// </summary>
    public IEnumerable<int> TrianglesAdjacentToTriangle(int t)
    {
        var adjacentTriangles = new List<int>();
        var triangleEdges = EdgesOfTriangle(t);
        foreach (var e in triangleEdges)
        {
            var opposite = Halfedges[e];
            if (opposite >= 0)
                adjacentTriangles.Add(TriangleOfEdge(opposite));
        }

        return adjacentTriangles;
    }

    public static int NextHalfedge(int e) => e % 3 == 2 ? e - 2 : e + 1;
    public static int PreviousHalfedge(int e) => e % 3 == 0 ? e + 2 : e - 1;

    /// <summary>
    ///     Returns the three half-edges of a given triangle id.
    /// </summary>
    public static int[] EdgesOfTriangle(int t) => new[] { 3 * t, 3 * t + 1, 3 * t + 2, };

    /// <summary>
    ///     Returns the triangle id of a given half-edge.
    /// </summary>
    public static int TriangleOfEdge(int e) => e / 3;

    #endregion Methods based on index
}
