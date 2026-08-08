using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using DiagnosticCatalog.CodeStyle;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A declared public name is matched before an unannotated member's C# name, and case-sensitively,
/// while the C# names themselves are matched case-insensitively. A collision therefore leaves the
/// shadowed member answering to every casing of its name except its own — which must be refused,
/// whatever the casing of the declared name.
/// </summary>
public sealed class ShadowedMemberTests {

    public enum ExactCasing {

        [JsonStringEnumMemberName("Blue")] Red,
        Blue

    }

    public enum LowerCasing {

        [JsonStringEnumMemberName("blue")] Red,
        Blue

    }

    public enum UpperCasing {

        [JsonStringEnumMemberName("BLUE")] Red,
        Blue

    }

    public enum NoCollision {

        [JsonStringEnumMemberName("crimson")] Red,
        Blue

    }

    [SuppressMessage(CodeStyleRule.IDE0028.Category, CodeStyleRule.IDE0028.Id,
                     Justification = SuppressionJustification.IDE0028.CollectionExpressionBreaksTheFloorSdk)]
    public static TheoryData<Type> Shadowing => new() { typeof(ExactCasing), typeof(LowerCasing), typeof(UpperCasing) };

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void a_shadowed_member_is_refused_whatever_the_casing(Type enumType) {
        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumContract.For(enumType));

        Assert.Equal(enumType, exception.EnumType);
        string problem = Assert.Single(exception.Problems);
        Assert.Contains("'Red'", problem, StringComparison.Ordinal);
        Assert.Contains("'Blue'", problem, StringComparison.Ordinal);
        Assert.Contains("casing", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void a_public_name_that_collides_with_nothing_is_accepted() {
        EnumContract contract = EnumContract.For(typeof(NoCollision));

        Assert.True(contract.TryParse("crimson", out object red));
        Assert.Equal(NoCollision.Red, red);
        Assert.True(contract.TryParse("Blue", out object blue));
        Assert.Equal(NoCollision.Blue, blue);
    }

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void the_application_refuses_to_start(Type enumType) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        EnumContractException exception = Assert.Throws<EnumContractException>(
            () => builder.Services.AddControllers().AddEnumMemberNameBinding(options => options.EnumTypes.Add(enumType)));

        Assert.Equal(enumType, exception.EnumType);
    }

    /// <summary>
    /// The collision is an ambiguity, not a policy choice, so opting into partial contracts does not
    /// make it acceptable.
    /// </summary>
    [Fact]
    public void allowing_partial_contracts_does_not_make_a_shadowed_member_acceptable() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        Assert.Throws<EnumContractException>(
            () => builder.Services.AddControllers().AddEnumMemberNameBinding(options => {
                options.EnumTypes.Add(typeof(LowerCasing));
                options.AllowPartialContracts = true;
            }));
    }

}
