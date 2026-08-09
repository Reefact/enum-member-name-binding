using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using DiagnosticCatalog.Trimming;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// A <see cref="TypeConverter" /> that resolves an enum from the member names declared with
/// <c>[JsonStringEnumMemberName]</c>.
/// </summary>
/// <remarks>
/// ASP.NET Core resolves simple-type model binders through <see cref="TypeDescriptor" />, so
/// registering this converter is enough to cover route values, query strings, form fields and
/// headers — including their nullable forms — without replacing any model binder.
/// </remarks>
/// <remarks>
/// Internal, deliberately. Public, the type advertised a second way in — writing
/// <c>[TypeConverter(typeof(EnumMemberNameConverter))]</c> on an enum by hand — whose guarantees
/// are strictly weaker than the documented one: it installs on an enum carrying no contract at
/// all, the adoption <see cref="EnumMemberNameBindingRegistry" /> exists to refuse; it skips the
/// System.Text.Json alignment that closes the body-versus-query divergence this package was
/// written for; and it surfaces <see cref="EnumContractException" /> wrapped in a
/// <c>TargetInvocationException</c> on the first request, contradicting that exception's promise
/// to be raised at start-up and never on a request. The supported entry point is
/// <c>AddEnumMemberNameBinding()</c>.
///
/// The constructor stays public even though the type is not: <c>TypeDescriptor</c> instantiates
/// converters reflectively through a public constructor, and making it internal fails at run time
/// with <c>MissingMethodException</c>. Publishing the type again later would be additive and free;
/// withdrawing it after v1 would not.
/// </remarks>
internal sealed class EnumMemberNameConverter : EnumConverter {

    private readonly EnumContract _contract;

    /// <summary>Creates a converter for <paramref name="type" />.</summary>
    /// <param name="type">The enum type to convert.</param>
    /// <remarks>
    /// The constructor itself only reads metadata, but <see cref="ConvertTo" /> reaches the
    /// System.Text.Json round trip and, overriding a base member, cannot carry the annotation. The
    /// requirement is therefore declared here, where a consumer can see it.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    public EnumMemberNameConverter(Type type) : base(NotNull(type)) {
        _contract = EnumContract.For(type);
    }

    private static Type NotNull(Type type) {
        ArgumentNullException.ThrowIfNull(type);

        return type;
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        ArgumentNullException.ThrowIfNull(value);

        if (value is string text) {
            if (_contract.TryParse(text, out object? result)) { return result; }

            throw new FormatException(NotAValidValue(text));
        }

        return base.ConvertFrom(context, culture, value);
    }

    private string NotAValidValue(string text) {
        return $"'{text}' is not a valid value for {_contract.EnumType.Name}. Allowed values: {_contract.AllowedValues}.";
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id,
        Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
    [UnconditionalSuppressMessage(TrimRule.IL3050.Category, TrimRule.IL3050.Id,
        Justification = SuppressionJustification.IL3050.RequirementCarriedByConstructor)]
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        ArgumentNullException.ThrowIfNull(destinationType);

        if (destinationType == typeof(string) && value is not null) {
            string? name = _contract.Format(value);
            if (name is not null) { return name; }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

}
