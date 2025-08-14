using System.Diagnostics.CodeAnalysis;


namespace Content.Client._SVGRenderer;


public sealed class MiniXmlElement
{
    private readonly List<MiniXmlElement> _children = new();
    private readonly Dictionary<string, int> _childIdAcc = new();

    public string Name = default!;
    public Dictionary<string, string> Attributes = new();
    public Dictionary<string, object> NonSerializedAttributes = new();
    public IEnumerable<MiniXmlElement> Children => _children;
    public MiniXmlElement? Parent { get; private set; }

    public void AddChild(MiniXmlElement element)
    {
        if(element.Parent != null)
            throw new ArgumentException("Element already has a parent");

        _children.Add(element);
        element.Parent = this;
        if (element.TryGetAttribute("id", out var id))
            _childIdAcc[id] = _children.Count - 1;
    }

    public bool TryGetNonSerializedAttribute<T>(string name,[NotNullWhen(true)] out T? value, bool searchParent = false)
    {
        if (NonSerializedAttributes.TryGetValue(name, out var valueRaw) && valueRaw is T tvalue)
        {
            value = tvalue;
            return true;
        }

        if(Parent != null && searchParent)
            return Parent.TryGetNonSerializedAttribute<T>(name, out value, searchParent);

        value = default;
        return false;
    }

    public string GetAttribute(string s) =>
        Attributes[s];

    public bool HasAttribute(string s) =>
        Attributes.TryGetValue(s, out _);

    public bool TryGetAttribute(string s,[NotNullWhen(true)] out string? value) =>
        Attributes.TryGetValue(s, out value);

    public bool TryGetChildById(string id,[NotNullWhen(true)] out MiniXmlElement? child) =>
        TryGetChildById(new Queue<string>(id.Split(";")), out child);

    public bool TryGetChildById(Queue<string> ids, [NotNullWhen(true)] out MiniXmlElement? child)
    {
        child = null;
        if (!ids.TryDequeue(out var id))
        {
            child = this;
            return true;
        }

        if (!_childIdAcc.TryGetValue(id, out int childId))
            return false;

        return _children[childId].TryGetChildById(ids, out child);
    }
}
