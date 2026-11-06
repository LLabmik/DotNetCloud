using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Tests.Middleware;

/// <summary>
/// A feature collection that fails the first <see cref="Set{TFeature}"/> call, simulating a
/// feature that cannot be installed. Used to exercise the middleware's fail-open path.
/// </summary>
internal sealed class ThrowFirstSetFeatureCollection(IFeatureCollection inner) : IFeatureCollection
{
    private bool _thrown;

    public object? this[Type key]
    {
        get => inner[key];
        set => inner[key] = value;
    }

    public bool IsReadOnly => inner.IsReadOnly;

    public int Revision => inner.Revision;

    public TFeature? Get<TFeature>() => inner.Get<TFeature>();

    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => inner.GetEnumerator();

    public void Set<TFeature>(TFeature? instance)
    {
        if (!_thrown)
        {
            _thrown = true;
            throw new InvalidOperationException("Simulated feature-install failure.");
        }

        inner.Set(instance);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
