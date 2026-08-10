using System.Text.Json.Serialization;

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

    public static TheoryData<Type> Shadowing => new() { typeof(ExactCasing), typeof(LowerCasing), typeof(UpperCasing) };

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void a_shadowed_member_is_refused_whatever_the_casing(Type enumType) {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(enumType)).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(enumType);
        Check.That(exception.Problems).HasOneElementOnly();
        string problem = exception.Problems.Single();
        Check.That(problem).Contains("'Red'");
        Check.That(problem).Contains("'Blue'");
        Check.That(problem).Contains("casing");
    }

    [Fact]
    public void a_public_name_that_collides_with_nothing_is_accepted() {
        EnumContract contract = EnumContract.For(typeof(NoCollision));

        Check.That(contract.TryParse("crimson", out object? red)).IsTrue();
        Check.That(red).IsEqualTo(NoCollision.Red);
        Check.That(contract.TryParse("Blue", out object? blue)).IsTrue();
        Check.That(blue).IsEqualTo(NoCollision.Blue);
    }

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void the_application_refuses_to_start(Type enumType) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        EnumContractException exception = Check.ThatCode(() => builder.Services.AddControllers().AddEnumMemberNameBinding(options => options.EnumTypes.Add(enumType)))
                                               .Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(enumType);
    }

    /// <summary>
    /// The collision is an ambiguity, not a policy choice, so opting into partial contracts does not
    /// make it acceptable.
    /// </summary>
    [Fact]
    public void allowing_partial_contracts_does_not_make_a_shadowed_member_acceptable() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        Check.ThatCode(() => builder.Services.AddControllers().AddEnumMemberNameBinding(options => { options.EnumTypes.Add(typeof(LowerCasing)); options.AllowPartialContracts = true; }))
             .Throws<EnumContractException>();
    }

}
