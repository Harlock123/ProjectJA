// SPDX-License-Identifier: BUSL-1.1
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace ProjectJA.ArchitectureTests;

public class ModuleBoundaryTests
{
    private static readonly string[] Modules =
    [
        "ProjectJA.Modules.Identity",
        "ProjectJA.Modules.Projects",
        "ProjectJA.Modules.Issues",
        "ProjectJA.Modules.Workflows",
        "ProjectJA.Modules.Boards",
        "ProjectJA.Modules.Search",
        "ProjectJA.Modules.Notifications",
        "ProjectJA.Modules.Audit",
    ];

    // Modules may reference another module's Contracts namespace, but never its
    // Domain, Application, Persistence, or Endpoints internals.
    [Fact]
    public void Modules_must_not_reference_other_modules_internals()
    {
        LoadAllModuleAssemblies();

        foreach (var self in Modules)
        {
            var forbidden = Modules
                .Where(other => other != self)
                .SelectMany(other => new[]
                {
                    $"{other}.Domain",
                    $"{other}.Application",
                    $"{other}.Persistence",
                    $"{other}.Endpoints",
                })
                .ToArray();

            var result = Types.InCurrentDomain()
                .That()
                .ResideInNamespaceStartingWith(self)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"Module {self} reaches into internal namespaces of another module. Offenders: "
                + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        }
    }

    private static void LoadAllModuleAssemblies()
    {
        // Force every module assembly into the AppDomain so NetArchTest can see them.
        foreach (var module in Modules)
        {
            try { Assembly.Load(module); }
            catch (FileNotFoundException) { /* module not present in test deps; skip */ }
        }
    }
}
