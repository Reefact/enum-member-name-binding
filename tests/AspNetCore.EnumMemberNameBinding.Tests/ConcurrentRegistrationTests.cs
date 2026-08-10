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

    private const int Threads = 8;

    /// <summary>
    /// Dedicated threads rather than <c>Parallel.For</c>: a barrier of N needs N threads running at
    /// once, and the thread pool injects them one every 250 ms or so, which turns each case into
    /// seconds of waiting and the suite into a minute.
    /// </summary>
    private static void RaceOn(Action<int> body) {
        using Barrier gate = new(Threads);
        Exception?[] failures = new Exception?[Threads];

        Thread[] threads = [.. Enumerable.Range(0, Threads).Select(index => new Thread(() => {
            try {
                gate.SignalAndWait();
                body(index);
            } catch (Exception exception) {
                failures[index] = exception;
            }
        }) { IsBackground = true })];

        foreach (Thread thread in threads) { thread.Start(); }
        foreach (Thread thread in threads) { thread.Join(); }

        Exception? failure = failures.FirstOrDefault(f => f is not null);
        if (failure is not null) { throw failure; }
    }

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
        Type[] observed = new Type[Threads];

        RaceOn(index => {
            EnumMemberNameBindingOptions options = new();
            options.EnumTypes.Add(enumType);

            EnumMemberNameBindingRegistry.Register(options);

            observed[index] = TypeDescriptor.GetConverter(enumType).GetType();
        });

        Check.That(observed).ContainsOnlyElementsThatMatch(converter => converter == typeof(EnumMemberNameConverter));
    }

    [Theory]
    [MemberData(nameof(Racers))]
    public void a_concurrent_race_leaves_a_single_usable_registration(Type enumType) {
        RaceOn(_ => {
            EnumMemberNameBindingOptions options = new();
            options.EnumTypes.Add(enumType);

            EnumMemberNameBindingRegistry.Register(options);
        });

        TypeConverter converter = TypeDescriptor.GetConverter(enumType);

        Check.That(converter).IsInstanceOf<EnumMemberNameConverter>();
        Check.That(converter.ConvertFromString("b")).IsEqualTo(Enum.Parse(enumType, "B"));
        Check.ThatCode(() => converter.ConvertFromString("B")).Throws<FormatException>();
    }

    [Fact]
    public void the_contract_cache_is_safe_under_concurrent_first_use() {
        EnumContract[] resolved = new EnumContract[Threads];

        RaceOn(index => resolved[index] = EnumContract.For(typeof(ProductStatus)));

        // GetOrAdd may build the value more than once under contention; every result must still be
        // equivalent, and every later caller must observe the one that won.
        Check.That(resolved).ContainsOnlyElementsThatMatch(contract => contract.AllowedValues == "available, out_of_stock, discontinued");
        Check.That(EnumContract.For(typeof(ProductStatus))).IsSameReferenceAs(EnumContract.For(typeof(ProductStatus)));
    }

}
