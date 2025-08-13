namespace Content.Client._SVGRenderer;


public static class MiniXmlParser
{
    public static MiniXmlElement Parse(string xml)
    {
        var pos = 0;
        xml = xml.Replace("\n", "");
        SkipDeclaration(xml, ref pos);
        return ParseElement(xml, ref pos);
    }

    private static void SkipDeclaration(string xml, ref int pos)
    {
        if (xml.StartsWith("<?"))
        {
            var end = xml.IndexOf("?>", pos, StringComparison.Ordinal);
            pos = (end >= 0) ? end + 2 : pos;
        }

        SkipWhitespace(xml, ref pos);

        if (xml.Substring(pos).StartsWith("<!--"))
        {
            var end = xml.IndexOf("-->", pos, StringComparison.Ordinal);
            pos = (end >= 0) ? end + 3 : pos;
        }
    }

    private static MiniXmlElement ParseElement(string xml, ref int pos)
    {
        SkipWhitespace(xml, ref pos);
        if (pos >= xml.Length || xml[pos] != '<')
        {
           throw new Exception("Invalid XML element");
        }
        pos++;

        // Read tag name
        var name = ReadName(xml, ref pos);
        var el = new MiniXmlElement { Name = name };

        // Read attributes
        while (true)
        {
            SkipWhitespace(xml, ref pos);
            if (pos >= xml.Length) break;
            if (xml[pos] == '/' || xml[pos] == '>') break;

            var attrName = ReadName(xml, ref pos);
            SkipWhitespace(xml, ref pos);
            var attrValue = "";
            if (pos < xml.Length && xml[pos] == '=')
            {
                pos++;
                SkipWhitespace(xml, ref pos);
                attrValue = ReadAttributeValue(xml, ref pos);
            }
            el.Attributes[attrName] = attrValue;
        }

        // Self-closing tag
        if (pos < xml.Length && xml[pos] == '/')
        {
            while (pos < xml.Length && xml[pos] != '>') pos++;
            if (pos < xml.Length) pos++;
            return el;
        }

        // Skip '>'
        if (pos < xml.Length && xml[pos] == '>') pos++;

        // Read children until </name>
        while (true)
        {
            SkipWhitespace(xml, ref pos);
            if (pos >= xml.Length) break;
            if (xml[pos] == '<')
            {
                if (pos + 1 < xml.Length && xml[pos + 1] == '/')
                {
                    // Closing tag
                    pos += 2;
                    ReadName(xml, ref pos); // skip name
                    while (pos < xml.Length && xml[pos] != '>') pos++;
                    if (pos < xml.Length) pos++;
                    break;
                }
                else if (pos + 1 < xml.Length && xml[pos + 1] == '!')
                {
                    // Comment or doctype — skip
                    var end = xml.IndexOf('>', pos + 2);
                    pos = (end >= 0) ? end + 1 : xml.Length;
                }
                else
                {
                    var child = ParseElement(xml, ref pos);
                    if (child != null) el.AddChild(child);
                }
            }
            else
            {
                // Text content (ignored for now)
                while (pos < xml.Length && xml[pos] != '<') pos++;
            }
        }

        return el;
    }

    private static string ReadName(string xml, ref int pos)
    {
        var start = pos;
        while (pos < xml.Length && (char.IsLetterOrDigit(xml[pos]) || xml[pos] == ':' || xml[pos] == '_' || xml[pos] == '-'))
            pos++;
        return xml.Substring(start, pos - start);
    }

    private static string ReadAttributeValue(string xml, ref int pos)
    {
        if (pos >= xml.Length) return "";
        var quote = xml[pos];
        if (quote == '"' || quote == '\'')
        {
            pos++;
            var start = pos;
            while (pos < xml.Length && xml[pos] != quote) pos++;
            var val = xml.Substring(start, pos - start);
            if (pos < xml.Length) pos++;
            return val;
        }
        else
        {
            // Unquoted value
            var start = pos;
            while (pos < xml.Length && !char.IsWhiteSpace(xml[pos]) && xml[pos] != '>' && xml[pos] != '/')
                pos++;
            return xml.Substring(start, pos - start);
        }
    }

    private static void SkipWhitespace(string xml, ref int pos)
    {
        while (pos < xml.Length && char.IsWhiteSpace(xml[pos])) pos++;
    }
}
