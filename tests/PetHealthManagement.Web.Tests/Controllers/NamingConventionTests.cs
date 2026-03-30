using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace PetHealthManagement.Web.Tests.Controllers;

public class NamingConventionTests
{
    private static readonly Regex LowerCamelCasePattern = new("^[a-z][A-Za-z0-9]*$", RegexOptions.Compiled);
    private static readonly Regex RouteTokenPattern = new(@"\{(?<name>[^}:=?]+)", RegexOptions.Compiled);

    [Fact]
    public void ActionRouteTemplates_UseLowerCamelCasePlaceholders()
    {
        var violations = GetActionMethods()
            .SelectMany(method => method
                .GetCustomAttributes(inherit: true)
                .OfType<IRouteTemplateProvider>()
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Template))
                .SelectMany(attribute => ExtractRouteTokens(attribute.Template!))
                .Where(token => !IsLowerCamelCase(token))
                .Select(token => $"{method.DeclaringType!.Name}.{method.Name}: {token}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ActionParameters_UseLowerCamelCaseNames()
    {
        var violations = GetActionMethods()
            .SelectMany(method => method
                .GetParameters()
                .Where(parameter => parameter.Name is not null && !IsLowerCamelCase(parameter.Name))
                .Select(parameter => $"{method.DeclaringType!.Name}.{method.Name}: {parameter.Name}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<MethodInfo> GetActionMethods()
    {
        return typeof(Program).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && typeof(Controller).IsAssignableFrom(type)
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any());
    }

    private static IEnumerable<string> ExtractRouteTokens(string template)
    {
        return RouteTokenPattern
            .Matches(template)
            .Select(match => match.Groups["name"].Value)
            .Where(static token =>
                !string.IsNullOrWhiteSpace(token)
                && !string.Equals(token, "controller", StringComparison.Ordinal)
                && !string.Equals(token, "action", StringComparison.Ordinal)
                && !string.Equals(token, "area", StringComparison.Ordinal));
    }

    private static bool IsLowerCamelCase(string value)
    {
        return LowerCamelCasePattern.IsMatch(value);
    }
}
