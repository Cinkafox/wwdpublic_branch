using System.Collections.Concurrent;


namespace Content.RobustOAuth.Server.Utils;


public sealed class GuidPool
{
    private readonly ConcurrentQueue<Guid> _pool = new();

    public GuidPool(int initialCapacity = 0)
    {
        for (var i = 0; i < initialCapacity; i++)
        {
            _pool.Enqueue(Guid.NewGuid());
        }
    }

    public Guid Take() =>
        _pool.TryDequeue(out var guid) ? guid : Guid.NewGuid();

    public void Free(Guid guid)
    {
        if (guid == Guid.Empty)
            return;

        _pool.Enqueue(guid);
    }

    public GuidRental Rent()
    {
        return new GuidRental(Take(), this);
    }
}

public sealed class GuidRental : IDisposable
{
    public Guid Value { get; }
    private GuidPool? _pool;

    internal GuidRental(Guid value, GuidPool pool)
    {
        Value = value;
        _pool = pool;
    }

    public void Dispose()
    {
        var pool = _pool;
        if (pool != null)
        {
            _pool = null;
            pool.Free(Value);
        }
    }
}
