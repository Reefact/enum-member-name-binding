using System.Text.Json.Serialization;

// This project is MEANT TO FAIL TO COMPILE. run.sh asserts that it does, and that the reason is
// EMN0003 — which can only be reported by the analyzer shipped inside the .nupkg, since nothing
// here references the analyzer project.
//
// The assertion is deliberately the positive one. "No diagnostic appeared" is what you also get
// when the analyzer never loaded at all, so a test written that way would pass most loudly at the
// exact moment the packaging broke.

/// <summary>
/// Declares a contract on one member and leaves the other bare, which puts the C# name
/// <c>Discontinued</c> into the public API. EMN0003, an error.
/// </summary>
public enum ProductStatus {

    [JsonStringEnumMemberName("available")] Available,
    Discontinued

}

public static class Program {

    public static void Main() { }

}
