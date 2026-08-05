using System.ComponentModel;
using System.Globalization;

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
public sealed class EnumMemberNameConverter : EnumConverter {

    private readonly EnumContract _contract;

    /// <summary>Creates a converter for <paramref name="type" />.</summary>
    /// <param name="type">The enum type to convert.</param>
    public EnumMemberNameConverter(Type type) : base(type) {
        _contract = EnumContract.For(type);
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if (value is string text) {
            if (_contract.TryParse(text, out object result)) { return result; }

            throw new FormatException(
                $"'{text}' is not a valid value for {_contract.EnumType.Name}. Allowed values: {_contract.AllowedValues}.");
        }

        return base.ConvertFrom(context!, culture, value);
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        ArgumentNullException.ThrowIfNull(destinationType);

        if (destinationType == typeof(string) && value is not null) {
            string? name = _contract.Format(value);
            if (name is not null) { return name; }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

}
