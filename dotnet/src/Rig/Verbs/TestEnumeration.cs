using System.Reflection;

namespace Rig;

/// <summary>
/// Enumerates test classes from a built test assembly via reflection-only
/// metadata loading (no test-host spin-up). Multi-framework: MSTest
/// (<c>[TestClass]</c>, including derived attributes via the base-chain walk),
/// NUnit (<c>[TestFixture]</c> or a <c>[Test]</c> method), and xUnit (a
/// <c>[Fact]</c>/<c>[Theory]</c> method). The classification predicate
/// (<see cref="IsTestClass"/>) is pure and unit-tested; the
/// <see cref="Enumerate"/> path is the integration over MetadataLoadContext.
/// </summary>
internal static class TestEnumeration
{
    private const string MsTestClass = "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute";
    private const string NUnitFixture = "NUnit.Framework.TestFixtureAttribute";

    private static readonly string[] TestMethodMarkers =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
    ];

    /// <param name="typeAttributes">Full names of attributes on the type and its
    /// base chain, each expanded along its own attribute base-chain (so a
    /// custom <c>[MyTestClass] : TestClassAttribute</c> contributes the MSTest name).</param>
    /// <param name="methodAttributes">Full names of attributes on the type's methods.</param>
    public static bool IsTestClass(
        IReadOnlyCollection<string> typeAttributes,
        IReadOnlyCollection<string> methodAttributes)
    {
        if (typeAttributes.Contains(MsTestClass)) return true;
        if (typeAttributes.Contains(NUnitFixture)) return true;
        return methodAttributes.Any(a => TestMethodMarkers.Contains(a));
    }

    public static IReadOnlyList<string> Enumerate(string testDllPath)
    {
        if (string.IsNullOrEmpty(testDllPath) || !File.Exists(testDllPath)) return [];

        // Resolver: the host runtime (provides the core assembly + BCL by simple
        // name) plus the test's own bin dir (its framework references + deps).
        // MetadataLoadContext reads metadata only, so a lower-TFM host can read a
        // higher-TFM assembly — name-level resolution is enough.
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var binDir = Path.GetDirectoryName(testDllPath)!;
        var paths = Directory.GetFiles(runtimeDir, "*.dll")
            .Concat(Directory.GetFiles(binDir, "*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));

        Type[] types;
        try
        {
            var asm = mlc.LoadFromAssemblyPath(testDllPath);
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }
        catch
        {
            return [];
        }

        var result = new List<string>();
        foreach (var t in types)
        {
            if (!t.IsClass || t.IsAbstract) continue;
            if (IsTestClass(CollectTypeAttributes(t), CollectMethodAttributes(t)))
                result.Add(t.FullName ?? t.Name);
        }
        return result.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    private static HashSet<string> CollectTypeAttributes(Type type)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var cur = type; cur is not null; cur = SafeBase(cur))
            foreach (var data in SafeAttrs(cur))
                AddChain(set, data.AttributeType);
        return set;
    }

    private static HashSet<string> CollectMethodAttributes(Type type)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        MethodInfo[] methods;
        try { methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
        catch { return set; }

        foreach (var m in methods)
        {
            IList<CustomAttributeData> attrs;
            try { attrs = m.GetCustomAttributesData(); }
            catch { continue; }
            foreach (var a in attrs) AddChain(set, a.AttributeType);
        }
        return set;
    }

    private static void AddChain(HashSet<string> set, Type? attributeType)
    {
        for (var at = attributeType; at is not null; at = SafeBase(at))
            if (at.FullName is not null) set.Add(at.FullName);
    }

    private static Type? SafeBase(Type t)
    {
        try { return t.BaseType; }
        catch { return null; } // base assembly unresolved in the metadata context
    }

    private static IList<CustomAttributeData> SafeAttrs(Type t)
    {
        try { return t.GetCustomAttributesData(); }
        catch { return []; }
    }
}
