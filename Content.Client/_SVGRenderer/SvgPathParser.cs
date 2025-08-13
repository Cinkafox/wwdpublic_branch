using System.Globalization;

namespace Content.Client._SVGRenderer
{
    public static class SvgPathParser
    {
        public sealed class SvgCmd
        {
            public char Cmd;
            public float[] Params = Array.Empty<float>();
        }

        public static List<SvgCmd> ParsePath(string d)
        {
            var result = new List<SvgCmd>();
            if (string.IsNullOrWhiteSpace(d))
                return result;

            var span = d.AsSpan();
            var i = 0;
            var currentCmd = '\0';

            while (i < span.Length)
            {
                var c = span[i];

                // Command letter
                if (char.IsLetter(c))
                {
                    currentCmd = c;
                    i++;
                }
                else if (currentCmd == '\0')
                {
                    // Skip until first command
                    i++;
                    continue;
                }

                var expected = ExpectedParams(currentCmd);
                var p = new List<float>(8);

                // Read parameters
                while (i < span.Length && !char.IsLetter(span[i]))
                {
                    if (TryReadFloat(span, ref i, out var val))
                        p.Add(val);
                    else
                        i++;
                }

                if (expected == 0)
                {
                    result.Add(new SvgCmd { Cmd = currentCmd });
                }
                else
                {
                    // Chunk into groups
                    for (var idx = 0; idx + expected <= p.Count; idx += expected)
                    {
                        var arr = new float[expected];
                        p.CopyTo(idx, arr, 0, expected);
                        result.Add(new SvgCmd { Cmd = currentCmd, Params = arr });
                    }
                }
            }

            return result;
        }

        private static bool TryReadFloat(ReadOnlySpan<char> span, ref int i, out float value)
        {
            value = 0;
            var start = i;
            var hasDigit = false;

            // Optional sign
            if (i < span.Length && (span[i] == '+' || span[i] == '-'))
                i++;

            // Digits before decimal
            while (i < span.Length && char.IsDigit(span[i]))
            {
                hasDigit = true;
                i++;
            }

            // Decimal point
            if (i < span.Length && span[i] == '.')
            {
                i++;
                while (i < span.Length && char.IsDigit(span[i]))
                {
                    hasDigit = true;
                    i++;
                }
            }

            // Scientific notation
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

        private static int ExpectedParams(char cmd)
        {
            switch (char.ToUpperInvariant(cmd))
            {
                case 'Z': return 0;
                case 'H': return 1;
                case 'V': return 1;
                case 'M': return 2;
                case 'L': return 2;
                case 'T': return 2;
                case 'S': return 4;
                case 'Q': return 4;
                case 'C': return 6;
                case 'A': return 7;
                default:  return 0;
            }
        }
    }
}
