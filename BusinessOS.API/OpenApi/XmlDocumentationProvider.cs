using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;

namespace BusinessOS.API.OpenApi;

/// <summary>
/// Loads and queries XML documentation files generated at build time for referenced assemblies.
/// </summary>
public sealed class XmlDocumentationProvider
{
    private readonly Dictionary<string, XPathNavigator> _memberNavigators = new(StringComparer.Ordinal);
    private readonly Dictionary<string, XPathNavigator> _typeNavigators = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a provider that indexes XML documentation from the given assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies whose companion .xml files should be loaded from the output directory.</param>
    public XmlDocumentationProvider(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies.Distinct())
        {
            LoadAssemblyDocumentation(assembly);
        }
    }

    /// <summary>
    /// Returns the summary text for a type, or null when no XML documentation exists.
    /// </summary>
    public string? GetTypeSummary(Type type) =>
        GetSummary(GetTypeMemberName(type));

    /// <summary>
    /// Returns the summary text for a method, or null when no XML documentation exists.
    /// </summary>
    public string? GetMethodSummary(MethodInfo method) =>
        GetSummary(GetMethodMemberName(method));

    /// <summary>
    /// Returns remarks for a documented member.
    /// </summary>
    public string? GetRemarks(string memberName)
    {
        if (!_memberNavigators.TryGetValue(memberName, out var navigator))
        {
            return null;
        }

        return navigator.SelectSingleNode("remarks")?.Value?.Trim();
    }

    /// <summary>
    /// Returns the combined summary and remarks for OpenAPI descriptions.
    /// </summary>
    public string? GetFullDescription(string memberName)
    {
        if (!_memberNavigators.TryGetValue(memberName, out var navigator))
        {
            return null;
        }

        var summary = navigator.SelectSingleNode("summary")?.Value?.Trim();
        var remarks = navigator.SelectSingleNode("remarks")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(remarks))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(remarks))
        {
            return summary;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return remarks;
        }

        return $"{summary}\n\n{remarks}";
    }

    private string? GetSummary(string memberName)
    {
        if (!_memberNavigators.TryGetValue(memberName, out var navigator))
        {
            return null;
        }

        return navigator.SelectSingleNode("summary")?.Value?.Trim();
    }

    private void LoadAssemblyDocumentation(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return;
        }

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
        if (!File.Exists(xmlPath))
        {
            return;
        }

        try
        {
            var document = XDocument.Load(xmlPath);
            var root = document.Root;
            if (root is null)
            {
                return;
            }

            foreach (var member in root.Elements("members").Elements("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var navigator = member.CreateNavigator();
                if (navigator is null)
                {
                    continue;
                }

                _memberNavigators[name] = navigator;

                if (name.StartsWith("T:", StringComparison.Ordinal))
                {
                    _typeNavigators[name[2..]] = navigator;
                }
            }
        }
        catch
        {
            // Documentation is best-effort; missing or malformed XML must not break the API.
        }
    }

    /// <summary>
    /// Builds the XML member name for a type (e.g. <c>T:Namespace.Type</c>).
    /// </summary>
    public static string GetTypeMemberName(Type type) =>
        $"T:{type.FullName?.Replace("+", ".", StringComparison.Ordinal)}";

    /// <summary>
    /// Builds the XML member name for a method (e.g. <c>M:Namespace.Type.Method</c>).
    /// </summary>
    public static string GetMethodMemberName(MethodInfo method)
    {
        var declaringType = method.DeclaringType?.FullName?.Replace("+", ".", StringComparison.Ordinal)
            ?? method.ReflectedType?.FullName?.Replace("+", ".", StringComparison.Ordinal)
            ?? "Unknown";

        if (!method.GetParameters().Any())
        {
            return $"M:{declaringType}.{method.Name}";
        }

        var parameterTypes = string.Join(",", method.GetParameters().Select(p => GetXmlTypeName(p.ParameterType)));
        return $"M:{declaringType}.{method.Name}({parameterTypes})";
    }

    private static string GetXmlTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName?.Replace("+", ".", StringComparison.Ordinal) ?? type.Name;
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        var genericName = genericDefinition.FullName?
            .Replace("+", ".", StringComparison.Ordinal)
            .Split('`')[0];

        var typeArgs = string.Join(",", type.GetGenericArguments().Select(GetXmlTypeName));
        return $"{genericName}{{{typeArgs}}}";
    }
}
