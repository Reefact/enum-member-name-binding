using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// A contract enum reached through a nullable collection element still admits <see langword="null" />,
/// and the document has to say so — replacing the values is not the same as replacing the type.
/// </summary>
/// <remarks>
/// The component describes the type wherever it appears, so the position that made it nullable is
/// not visible from the transformer. A nullable property is emitted as a <c>oneOf</c> of a null
/// schema and a reference, which leaves the component alone; a nullable collection element is not
/// wrapped, and the platform expresses it inside the component itself — pinned below by
/// <see cref="StockNullabilityTests" />, which is where the transformer reads it from.
/// </remarks>
[Collection(nameof(OpenApiCollection))]
public sealed class NullableSchemaTests(OpenApiTestApi api) {

    [Fact]
    public void a_nullable_contract_enum_is_typed_as_a_string_or_null() {
        string[] types = [.. api.Schema(nameof(Availability)).GetProperty("type").EnumerateArray().Select(type => type.GetString()!)];

        Check.That(types).Contains("string", "null");
    }

    /// <summary>
    /// The declared names, and the null beside them — dropped when the list was replaced wholesale.
    /// </summary>
    [Fact]
    public void a_nullable_contract_enum_advertises_its_public_names_and_null() {
        JsonElement[] values = [.. api.Schema(nameof(Availability)).GetProperty("enum").EnumerateArray()];

        Check.That(values.Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()))
             .ContainsExactly("available", "sold", null);
    }

    /// <summary>
    /// The non-nullable neighbour, so the fix is read as the narrower thing it is: a schema that
    /// admits no null gains neither the type nor the element.
    /// </summary>
    [Fact]
    public void an_ordinary_contract_enum_gains_neither_the_null_type_nor_the_null_value() {
        JsonElement schema = api.Schema(nameof(OrderState));

        Check.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        Check.That(schema.GetProperty("enum").EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Null)).IsFalse();
    }

    /// <summary>The point of the package, held on this shape too: the server answers what is advertised.</summary>
    [Fact]
    public async Task the_server_accepts_the_null_the_document_advertises() {
        Check.That(api.Schema(nameof(Availability)).GetProperty("enum").EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Null)).IsTrue();

        using HttpResponseMessage response = await api.Client.PostAsync(
            "/basket",
            JsonContent.Create(new { items = new string?[] { "available", null, "sold" } }),
            TestContext.Current.CancellationToken);

        Check.WithCustomMessage("the document advertises a null element but the server refused one.")
             .That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        JsonElement echoed = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Check.That(echoed.GetProperty("items").EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null ? null : item.GetString()))
             .ContainsExactly("available", null, "sold");
    }

}

/// <summary>
/// Where the null comes from. The transformer cannot see the position that made the schema nullable,
/// so it reads what the platform built before replacing it — and this pins the thing it reads.
/// </summary>
[Collection(nameof(StockOpenApiCollection))]
public sealed class StockNullabilityTests(WithoutTransformer api) {

    [Fact]
    public void stock_aspnetcore_expresses_a_nullable_element_as_a_null_inside_the_enum() {
        JsonElement schema = api.Schema(nameof(Availability));

        Check.That(schema.GetProperty("enum").EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Null)).IsTrue();
    }

}
