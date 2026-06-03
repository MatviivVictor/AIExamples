using System.Diagnostics;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class GuardrailsEvaluator
{
    private readonly InputGuardrailService _inputGuardrail;

    public GuardrailsEvaluator(InputGuardrailService inputGuardrail)
    {
        _inputGuardrail = inputGuardrail;
    }

    public async Task<EvaluationSuiteResult> EvaluateAsync(List<GuardrailsEvaluationCase> cases)
    {
        var suiteResult = new EvaluationSuiteResult
        {
            SuiteName = "Guardrails Evaluation",
            Tests = []
        };

        foreach (var testCase in cases)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = _inputGuardrail.Validate(testCase.Input);
            stopwatch.Stop();

            var testResult = new EvaluationTestResult
            {
                Id = testCase.Id,
                Description = testCase.Description,
                Duration = stopwatch.Elapsed,
                RawOutput = $"IsAllowed: {result.IsAllowed}, RiskLevel: {result.RiskLevel}"
            };

            if (result.IsAllowed != testCase.ExpectedIsAllowed)
            {
                testResult.Passed = false;
                testResult.FailureReason = $"IsAllowed mismatch: expected {testCase.ExpectedIsAllowed}, got {result.IsAllowed}";
            }
            else if (!string.Equals(result.RiskLevel.ToString(), testCase.ExpectedRiskLevel, StringComparison.OrdinalIgnoreCase))
            {
                testResult.Passed = false;
                testResult.FailureReason = $"RiskLevel mismatch: expected {testCase.ExpectedRiskLevel}, got {result.RiskLevel}";
            }
            else
            {
                testResult.Passed = true;
            }

            suiteResult.Tests.Add(testResult);
        }

        suiteResult.TotalTests = suiteResult.Tests.Count;
        suiteResult.PassedTests = suiteResult.Tests.Count(t => t.Passed);
        suiteResult.FailedTests = suiteResult.TotalTests - suiteResult.PassedTests;
        suiteResult.PassRate = suiteResult.TotalTests > 0 ? (double)suiteResult.PassedTests / suiteResult.TotalTests * 100 : 0;

        return suiteResult;
    }
}
