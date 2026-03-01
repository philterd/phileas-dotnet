using Phileas.Filters;

namespace Phileas.Services;

/// <summary>
/// Default in-memory implementation of <see cref="IContextService"/>.
/// Stores PII token → replacement value mappings in a thread-safe, in-memory dictionary
/// keyed by context name.
/// </summary>
public class InMemoryContextService : IContextService
{
    private readonly Dictionary<string, Dictionary<string, string>> _contexts = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string? Get(string contextName, string token)
    {
        lock (_lock)
        {
            if (_contexts.TryGetValue(contextName, out var ctx) &&
                ctx.TryGetValue(token, out var value))
                return value;
            return null;
        }
    }

    /// <inheritdoc/>
    public void Put(string contextName, string token, string replacement)
    {
        lock (_lock)
        {
            if (!_contexts.TryGetValue(contextName, out var ctx))
            {
                ctx = new Dictionary<string, string>();
                _contexts[contextName] = ctx;
            }
            ctx[token] = replacement;
        }
    }
}
