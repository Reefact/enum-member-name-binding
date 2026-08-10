using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
/// Registration writes into one application's container, but it reads from a cache the whole process
/// shares — the resolved contracts. Several applications starting at once is the ordinary case in a
/// test suite, so what that cache does under contention has to be known rather than hoped.
/// </summary>
/// <remarks>
/// It used to matter more. When the converter was installed through <c>TypeDescriptor</c>, a caller
/// returning before the installation had finished let a concurrent host cache a model binder built
/// on the stock converter, permanently. Nothing is shared for a race to be lost in any more, and
/// what is left here is the cache and the proof that two callers do not see each other.
/// </remarks>
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
    /// Every thread registers the same enum into a container of its own, and reads back a binding
    /// that works — the contract resolved once and shared, the provider installed once per
    /// container.
    /// </summary>
    [Theory]
    [MemberData(nameof(Racers))]
    public void every_concurrent_caller_gets_a_registration_of_its_own(Type enumType) {
        int[] providers = new int[Threads];

        RaceOn(index => {
            ServiceCollection services = new();
            services.AddControllers().AddEnumMemberNameBinding(options => options.EnumTypes.Add(enumType));

            providers[index] = OurProviders(services);
        });

        Check.That(providers).ContainsOnlyElementsThatMatch(count => count == 1);
    }

    /// <summary>
    /// The contract each of them resolved is the same object, and it parses. This is the assertion
    /// the cache exists for: eight callers, one resolution, no half-built descriptor observed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Racers))]
    public void a_concurrent_race_leaves_a_single_usable_contract(Type enumType) {
        EnumContract[] resolved = new EnumContract[Threads];

        RaceOn(index => {
            EnumMemberNameBindingOptions options = new();
            options.EnumTypes.Add(enumType);

            EnumMemberNameBindingRegistry.Register(options);

            resolved[index] = EnumContract.For(enumType);
        });

        Check.That(resolved).ContainsOnlyElementsThatMatch(contract => ReferenceEquals(contract, EnumContract.For(enumType)));
        Check.That(EnumContract.For(enumType).TryParse("b", out object? parsed)).IsTrue();
        Check.That(parsed).IsEqualTo(Enum.Parse(enumType, "B"));
        Check.That(EnumContract.For(enumType).TryParse("B", out object? refused)).IsFalse();
        Check.That(refused).IsNull();
    }

    private static int OurProviders(IServiceCollection services) {
        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<MvcOptions>>().Value
                       .ModelBinderProviders.Count(binder => binder is EnumMemberNameModelBinderProvider);
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
