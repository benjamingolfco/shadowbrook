using System.Text.RegularExpressions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Shadowbrook.Api.IntegrationTests;

public class StepOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(
        IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc =>
        {
            var name = tc.TestMethod.Method.Name;
            var match = Regex.Match(name, @"^Step(\d+)_");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        });
    }
}
