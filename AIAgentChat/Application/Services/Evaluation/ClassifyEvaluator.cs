using System.Diagnostics;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class ClassifyEvaluator
{
    private readonly ClassificationService _classificationService;

    public ClassifyEvaluator(ClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public async Task<EvaluationSuiteResult> EvaluateAsync(List<ClassifyEvaluationCase> cases)
    {
        var suiteResult = new EvaluationSuiteResult
        {
            SuiteName = "Classify Evaluation",
            Tests = []
        };

        foreach (var testCase in cases)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _classificationService.ClassifyAsync(testCase.Input);
            stopwatch.Stop();

            var testResult = new EvaluationTestResult
            {
                Id = testCase.Id,
                Description = testCase.Description,
                Duration = stopwatch.Elapsed,
                RawOutput = result.RawResponse
            };

            Validate(testCase, result, testResult);
            suiteResult.Tests.Add(testResult);
        }

        suiteResult.TotalTests = suiteResult.Tests.Count;
        suiteResult.PassedTests = suiteResult.Tests.Count(t => t.Passed);
        suiteResult.FailedTests = suiteResult.TotalTests - suiteResult.PassedTests;
        suiteResult.PassRate = suiteResult.TotalTests > 0 ? (double)suiteResult.PassedTests / suiteResult.TotalTests * 100 : 0;

        return suiteResult;
    }

    private void Validate(ClassifyEvaluationCase testCase, ClassificationResult result, EvaluationTestResult testResult)
    {
        if (!result.Success)
        {
            testResult.Passed = false;
            testResult.FailureReason = $"Classification failed: {result.Error}";
            return;
        }

        var classification = result.Classification!;

        if (!string.Equals(classification.Category, testCase.ExpectedCategory, StringComparison.OrdinalIgnoreCase))
        {
            testResult.Passed = false;
            testResult.FailureReason = $"Category mismatch: expected '{testCase.ExpectedCategory}', got '{classification.Category}'";
            return;
        }

        if (!string.Equals(classification.Priority, testCase.ExpectedPriority, StringComparison.OrdinalIgnoreCase))
        {
            testResult.Passed = false;
            testResult.FailureReason = $"Priority mismatch: expected '{testCase.ExpectedPriority}', got '{classification.Priority}'";
            return;
        }

        if (!string.Equals(classification.Sentiment, testCase.ExpectedSentiment, StringComparison.OrdinalIgnoreCase))
        {
            testResult.Passed = false;
            testResult.FailureReason = $"Sentiment mismatch: expected '{testCase.ExpectedSentiment}', got '{classification.Sentiment}'";
            return;
        }

        if (classification.ShouldEscalate != testCase.ExpectedShouldEscalate)
        {
            testResult.Passed = false;
            testResult.FailureReason = $"ShouldEscalate mismatch: expected '{testCase.ExpectedShouldEscalate}', got '{classification.ShouldEscalate}'";
            return;
        }

        testResult.Passed = true;
    }
}
