using System.Globalization;

namespace Content.Client._SVGRenderer;


public static class SvgTransformParser
{
    public enum TransformType
    {
        Translate,
        Rotate,
        Scale,
        SkewX,
        SkewY,
        Matrix
    }

    public sealed class TransformCmd
    {
        public TransformType Type;
        public float[] Params = Array.Empty<float>();
    }

    public static List<TransformCmd> ParseTransform(string transform)
    {
        var result = new List<TransformCmd>();
        if (string.IsNullOrWhiteSpace(transform))
            return result;

        var span = transform.AsSpan();
        var i = 0;

        while (i < span.Length)
        {
            SkipWhitespace(span, ref i);
            if (i >= span.Length) break;

            // Read transform name
            var startName = i;
            while (i < span.Length && char.IsLetter(span[i]))
                i++;
            if (startName == i) { i++; continue; }

            var name = span.Slice(startName, i - startName).ToString();
            if (!TryGetTransformType(name, out var type))
                throw new Exception($"Unknown transform type: {name}");

            SkipWhitespace(span, ref i);
            if (i >= span.Length || span[i] != '(')
                throw new Exception($"Expected '(' after transform '{name}'");

            i++; // skip '('

            var parameters = new List<float>(6);
            while (i < span.Length && span[i] != ')')
            {
                if (TryReadFloat(span, ref i, out var val))
                {
                    parameters.Add(val);
                }
                else
                {
                    // skip commas or whitespace
                    if (span[i] == ',' || char.IsWhiteSpace(span[i]))
                    {
                        i++;
                        continue;
                    }
                    throw new Exception($"Unexpected character in parameters: '{span[i]}'");
                }
            }

            if (i >= span.Length || span[i] != ')')
                throw new Exception($"Missing ')' for transform '{name}'");

            i++; // skip ')'

            result.Add(new TransformCmd
            {
                Type = type,
                Params = parameters.ToArray()
            });
        }

        return result;
    }

    private static void SkipWhitespace(ReadOnlySpan<char> span, ref int i)
    {
        while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
    }

    private static bool TryReadFloat(ReadOnlySpan<char> span, ref int i, out float value)
    {
        value = 0;
        var start = i;
        var hasDigit = false;

        if (i < span.Length && (span[i] == '+' || span[i] == '-'))
            i++;

        while (i < span.Length && char.IsDigit(span[i]))
        {
            hasDigit = true;
            i++;
        }

        if (i < span.Length && span[i] == '.')
        {
            i++;
            while (i < span.Length && char.IsDigit(span[i]))
            {
                hasDigit = true;
                i++;
            }
        }

        if (i < span.Length && (span[i] == 'e' || span[i] == 'E'))
        {
            i++;
            if (i < span.Length && (span[i] == '+' || span[i] == '-'))
                i++;
            while (i < span.Length && char.IsDigit(span[i]))
                i++;
        }

        if (!hasDigit)
            return false;

        var slice = span.Slice(start, i - start);
        return float.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetTransformType(string name, out TransformType type)
    {
        switch (name)
        {
            case "translate": type = TransformType.Translate; return true;
            case "rotate": type = TransformType.Rotate; return true;
            case "scale": type = TransformType.Scale; return true;
            case "skewX": type = TransformType.SkewX; return true;
            case "skewY": type = TransformType.SkewY; return true;
            case "matrix": type = TransformType.Matrix; return true;
            default: type = default; return false;
        }
    }
}
