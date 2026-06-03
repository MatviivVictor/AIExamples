using System.Diagnostics;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class RagEvaluator
{
    private readonly RagService _ragService;

    public RagEvaluator(RagService ragService)
    {
        _ragService = ragService;
    }

    public async Task<EvaluationSuiteResult> EvaluateAsync(List<RagEvaluationCase> cases)
    {
        var suiteResult = new EvaluationSuiteResult
        {
            SuiteName = "RAG Evaluation",
            Tests = []
        };

        foreach (var testCase in cases)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _ragService.AnswerAsync(testCase.Question);
            stopwatch.Stop();

            var testResult = new EvaluationTestResult
            {
                Id = testCase.Id,
                Description = testCase.Description,
                Duration = stopwatch.Elapsed,
                RawOutput = result.Answered ? result.Answer : result.CannotAnswerReason
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

    private void Validate(RagEvaluationCase testCase, RagAnswerResult result, EvaluationTestResult testResult)
    {
        // AI evaluation is probabilistic, so we use heuristic checks.
        
        if (testCase.ShouldAnswer)
        {
            if (!result.Answered)
            {
                testResult.Passed = false;
                testResult.FailureReason = $"Expected answer, but got: {result.CannotAnswerReason}";
                return;
            }

            // Перевірка через contains не ідеальна, бо LLM може перефразувати,
            // але для базового regression testing prompts це хороший старт.
            var missingKeywords = testCase.ExpectedAnswerContains
                .Where(keyword => !result.Answer.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (missingKeywords.Any())
            {
                testResult.Passed = false;
                testResult.FailureReason = $"Missing keywords: {string.Join(", ", missingKeywords)}";
                return;
            }

            if (!string.IsNullOrEmpty(testCase.ExpectedSource))
            {
                var sourceFound = result.RetrievedChunks.Any(c => c.Source.Contains(testCase.ExpectedSource, StringComparison.OrdinalIgnoreCase));
                if (!sourceFound)
                {
                    testResult.Passed = false;
                    testResult.FailureReason = $"Expected source '{testCase.ExpectedSource}' not found in retrieved chunks.";
                    return;
                }
            }
        }
        else
        {
            if (result.Answered && !result.Answer.Contains("Cannot answer", StringComparison.OrdinalIgnoreCase))
            {
                testResult.Passed = false;
                testResult.FailureReason = "Expected 'cannot answer' scenario, but AI provided a regular answer.";
                return;
            }
        }

        testResult.Passed = true;
    }
}
