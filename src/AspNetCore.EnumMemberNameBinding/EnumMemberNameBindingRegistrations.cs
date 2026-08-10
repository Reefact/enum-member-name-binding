using System.Collections.Concurrent;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// The contract enums one application registered, held in that application's own container.
/// </summary>
/// <remarks>
/// The answer to "is this enum one of ours?" has to be asked twice — once by the model binder
/// provider, deciding whether to bind a parameter by its declared names, and once by the OpenAPI
/// companion, deciding whether to describe a schema with them — and the two must never disagree.
/// A document promising names the binder does not accept is worse than no document: a generated
/// client sends requests the server refuses.
///
/// It is a service and not a static, which is the whole point. Several applications can share a
/// process, and what one of them registered is not the others' business.
/// </remarks>
internal sealed class EnumMemberNameBindingRegistrations {

    /// <summary>
    /// Written at start-up and read on every request. A concurrent dictionary rather than a set
    /// behind a lock, because the reads are the hot side and a caller that registers after the first
    /// request is outside the documented contract anyway — but must still not corrupt anything.
    /// </summary>
    private readonly ConcurrentDictionary<Type, bool> _types = new();

    internal void Add(IEnumerable<Type> enumTypes) {
        ArgumentNullException.ThrowIfNull(enumTypes);

        foreach (Type enumType in enumTypes) { _types[enumType] = true; }
    }

    /// <summary>Whether <paramref name="enumType" /> was registered by this application.</summary>
    internal bool Contains(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        return _types.ContainsKey(enumType);
    }

}
