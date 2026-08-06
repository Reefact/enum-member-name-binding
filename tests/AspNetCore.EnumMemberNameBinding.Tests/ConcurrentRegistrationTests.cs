using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

public enum Racer0 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer1 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer2 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer3 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer4 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer5 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer6 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }
public enum Racer7 { [JsonStringEnumMemberName("a")] A, [JsonStringEnumMemberName("b")] B }

/// <summary>
/// Registration installs a converter into process-wide state. Installing it once is not enough:
/// no caller may return before the installation has completed, or a host that started concurrently
/// would resolve the stock converter and cache a model binder built on it for good.
/// </summary>
public sealed class ConcurrentRegistrationTests {

    private const int Threads = 16;

    public static TheoryData<Type> Racers => new() {
        typeof(Racer0), typeof(Racer1), typeof(Racer2), typeof(Racer3),
        typeof(Racer4), typeof(Racer5), typeof(Racer6), typeof(Racer7)
    };

    /// <summary>
    /// Every thread checks, the instant its own registration call returns, that the converter is
    /// already in place — which is what a caller is entitled to assume.
    /// </summary>
    /// <remarks>
    /// This test does <b>not</b> prove the synchronisation. It was run against the earlier
    /// <c>ConcurrentDictionary.TryAdd</c> implementation, where a losing caller returned before the
    /// winner had finished installing the converter, and it passed there too — most likely because
    /// <see cref="TypeDescriptor" /> serialises its own readers against writers, so the observation
    /// blocks until the installation completes. The window is real by construction but was not
    /// reproducible without adding a test seam to production code, which is not worth it. The lock
    /// is what makes the invariant hold; this test guards against corruption, exceptions and
    /// duplicate registration under contention, not against that specific interleaving.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Racers))]
    public void every_concurrent_caller_sees_the_converter_installed_when_its_call_returns(Type enumType) {
        using Barrier gate = new(Threads);
        Type[] observed = new Type[Threads];

        Parallel.For(0, Threads, index => {
            EnumMemberNameBindingOptions options = new();
            options.EnumTypes.Add(enumType);

            gate.SignalAndWait();
            EnumMemberNameBindingRegistry.Register(options);

            observed[index] = TypeDescriptor.GetConverter(enumType).GetType();
        });

        Assert.All(observed, converter => Assert.Equal(typeof(EnumMemberNameConverter), converter));
    }

    [Theory]
    [MemberData(nameof(Racers))]
    public void a_concurrent_race_leaves_a_single_usable_registration(Type enumType) {
        using Barrier gate = new(Threads);

        Parallel.For(0, Threads, _ => {
            EnumMemberNameBindingOptions options = new();
            options.EnumTypes.Add(enumType);

            gate.SignalAndWait();
            EnumMemberNameBindingRegistry.Register(options);
        });

        TypeConverter converter = TypeDescriptor.GetConverter(enumType);

        Assert.IsType<EnumMemberNameConverter>(converter);
        Assert.Equal(Enum.Parse(enumType, "B"), converter.ConvertFromString("b"));
        Assert.Throws<FormatException>(() => converter.ConvertFromString("B"));
    }

    [Fact]
    public void the_contract_cache_is_safe_under_concurrent_first_use() {
        EnumContract[] resolved = new EnumContract[Threads];
        using Barrier gate = new(Threads);

        Parallel.For(0, Threads, index => {
            gate.SignalAndWait();
            resolved[index] = EnumContract.For(typeof(ProductStatus));
        });

        // GetOrAdd may build the value more than once under contention; every result must still be
        // equivalent, and every later caller must observe the one that won.
        Assert.All(resolved, contract => Assert.Equal("available, out_of_stock, discontinued", contract.AllowedValues));
        Assert.Same(EnumContract.For(typeof(ProductStatus)), EnumContract.For(typeof(ProductStatus)));
    }

}
