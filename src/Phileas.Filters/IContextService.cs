namespace Phileas.Filters;

/// <summary>
/// Provides referential integrity for the RANDOM_REPLACE filter strategy by
/// persisting PII token → replacement value mappings within named contexts.
/// </summary>
public interface IContextService
{
    /// <summary>
    /// Returns the replacement value previously stored for the given token in the
    /// specified context, or <see langword="null"/> if no value has been stored yet.
    /// </summary>
    string? Get(string contextName, string token);

    /// <summary>
    /// Stores a replacement value for the given token in the specified context.
    /// </summary>
    void Put(string contextName, string token, string replacement);
}
