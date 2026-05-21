namespace MDRAVA.Tests;

internal static partial class TestRegistry
{
    public static TestCase[] All { get; } = Build();

    private static TestCase[] Build()
    {
        List<TestCase> tests = [];
        tests.AddRange(FoundationAndConfig());
        tests.AddRange(AdminTlsCacheMetricsDiagnostics());
        tests.AddRange(Http2AndHttp3());
        tests.AddRange(ResilienceAndHttp1Proxy());
        tests.AddRange(OperatorRuntimeAndLimits());
        return tests.ToArray();
    }

    private static TestCase Test(string name, Func<Task> run, params string[] categories)
    {
        return new TestCase(name, run, TestTaxonomy.CanonicalCategories(categories));
    }

    private static Func<Task> Sync(Action test)
    {
        return () =>
        {
            test();
            return Task.CompletedTask;
        };
    }
}
