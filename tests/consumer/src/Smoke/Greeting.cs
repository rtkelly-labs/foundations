namespace Smoke;

/// <summary>Proves the shared settings build a project with warnings as errors and every analyzer on.</summary>
public static class Greeting
{
    /// <summary>Greets <paramref name="name"/>.</summary>
    /// <param name="name">Who to greet.</param>
    /// <returns>The greeting.</returns>
    public static string For(string name) => $"Hello, {name}.";
}
