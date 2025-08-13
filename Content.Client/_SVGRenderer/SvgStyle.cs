using System.Globalization;


namespace Content.Client._SVGRenderer;

public sealed class SvgStyle
{
    public Color? Fill;
    public Color? Stroke;
    public float StrokeWidth = 1f;

    public static SvgStyle FromElement(MiniXmlElement el)
    {
        var s = new SvgStyle();

        if (el.HasAttribute("fill"))
        {
            var v = el.GetAttribute("fill").Trim();
            if (v == "none") s.Fill = null;
            else s.Fill = ParseColor(v);
        }

        if (el.HasAttribute("stroke"))
        {
            var v = el.GetAttribute("stroke").Trim();
            if (v == "none") s.Stroke = null;
            else s.Stroke = ParseColor(v);
        }

        if (el.HasAttribute("stroke-width"))
        {
            if (float.TryParse(el.GetAttribute("stroke-width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
                s.StrokeWidth = w;
        }

        // inline style attribute "style"
        if (el.HasAttribute("style"))
        {
            var style = el.GetAttribute("style");
            var parts = style.Split(';');
            foreach (var kv in parts)
            {
                if (string.IsNullOrWhiteSpace(kv)) continue;
                var idx = kv.IndexOf(':');
                if (idx < 0) continue;
                var key = kv.Substring(0, idx).Trim();
                var val = kv.Substring(idx + 1).Trim();
                if (key == "fill") s.Fill = val == "none" ? (Color?)null : ParseColor(val);
                if (key == "stroke") s.Stroke = val == "none" ? (Color?)null : ParseColor(val);
                if (key == "stroke-width" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sw)) s.StrokeWidth = sw;
            }
        }

        return s;
    }

    public static SvgStyle Combine(SvgStyle parent, SvgStyle child)
    {
        var r = new SvgStyle();
        r.Fill = child.Fill ?? parent.Fill;
        r.Stroke = child.Stroke ?? parent.Stroke;
        r.StrokeWidth = child.StrokeWidth != 1f ? child.StrokeWidth : parent.StrokeWidth;
        return r;
    }

    private static Color ParseColor(string v)
    {
        v = v.Trim();
        if (v.StartsWith("#"))
        {
            return Color.FromHex(v);
        }
        if (v.StartsWith("rgb"))
        {
            var inner = v.Substring(v.IndexOf('(') + 1).TrimEnd(')');
            var parts = inner.Split(',');
            if (parts.Length >= 3 &&
                int.TryParse(parts[0], out var r) &&
                int.TryParse(parts[1], out var g) &&
                int.TryParse(parts[2], out var b))
            {
                return new(r, g, b);
            }
        }

        // fallback: try known named colors
        try
        {
            return Color.FromName(v);
        }
        catch { }

        return Color.Black;
    }
}
