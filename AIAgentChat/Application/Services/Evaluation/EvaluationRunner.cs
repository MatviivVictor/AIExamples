using AIAgentChat.Application.Models;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class EvaluationRunner
{
    private readonly AiOptions _aiOptions;
    private readonly EvaluationDatasetLoader _datasetLoader;
    private readonly EvaluationResultWriter _resultWriter;
    private readonly RagEvaluator _ragEvaluator;
    private readonly ClassifyEvaluator _classifyEvaluator;
    private readonly GuardrailsEvaluator _guardrailsEvaluator;

    public EvaluationRunner(
        AiOptions aiOptions,
        EvaluationDatasetLoader datasetLoader,
        EvaluationResultWriter resultWriter,
        RagEvaluator ragEvaluator,
        ClassifyEvaluator classifyEvaluator,
        GuardrailsEvaluator guardrailsEvaluator)
    {
        _aiOptions = aiOptions;
        _datasetLoader = datasetLoader;
        _resultWriter = resultWriter;
        _ragEvaluator = ragEvaluator;
        _classifyEvaluator = classifyEvaluator;
        _guardrailsEvaluator = guardrailsEvaluator;
    }

    public async Task RunRagEvaluationAsync()
    {
        var cases = _datasetLoader.LoadRagCases();
        var runResult = StartRun();
        var suiteResult = await _ragEvaluator.EvaluateAsync(cases);
        runResult.Suites.Add(suiteResult);
        CompleteRun(runResult);
    }

    public async Task RunClassifyEvaluationAsync()
    {
        var cases = _datasetLoader.LoadClassifyCases();
        var runResult = StartRun();
        var suiteResult = await _classifyEvaluator.EvaluateAsync(cases);
        runResult.Suites.Add(suiteResult);
        CompleteRun(runResult);
    }

    public async Task RunGuardrailsEvaluationAsync()
    {
        var cases = _datasetLoader.LoadGuardrailsCases();
        var runResult = StartRun();
        var suiteResult = await _guardrailsEvaluator.EvaluateAsync(cases);
        runResult.Suites.Add(suiteResult);
        CompleteRun(runResult);
    }

    public async Task RunAllEvaluationsAsync()
    {
        var runResult = StartRun();

        var ragCases = _datasetLoader.LoadRagCases();
        runResult.Suites.Add(await _ragEvaluator.EvaluateAsync(ragCases));

        var classifyCases = _datasetLoader.LoadClassifyCases();
        runResult.Suites.Add(await _classifyEvaluator.EvaluateAsync(classifyCases));

        var guardrailsCases = _datasetLoader.LoadGuardrailsCases();
        runResult.Suites.Add(await _guardrailsEvaluator.EvaluateAsync(guardrailsCases));

        CompleteRun(runResult);
    }

    private EvaluationRunResult StartRun()
    {
        return new EvaluationRunResult
        {
            RunId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTimeOffset.Now,
            Provider = _aiOptions.Provider,
            Model = _aiOptions.Model,
            Suites = []
        };
    }

    private void CompleteRun(EvaluationRunResult runResult)
    {
        runResult.FinishedAt = DateTimeOffset.Now;
        runResult.TotalTests = runResult.Suites.Sum(s => s.TotalTests);
        runResult.PassedTests = runResult.Suites.Sum(s => s.PassedTests);
        runResult.FailedTests = runResult.Suites.Sum(s => s.FailedTests);
        runResult.PassRate = runResult.TotalTests > 0 ? (double)runResult.PassedTests / runResult.TotalTests * 100 : 0;

        var filePath = _resultWriter.SaveResult(runResult);

        Console.WriteLine();
        Console.WriteLine("Evaluation completed.");
        foreach (var suite in runResult.Suites)
        {
            Console.WriteLine($"Suite: {suite.SuiteName}");
            Console.WriteLine($"  Tests: {suite.TotalTests}, Passed: {suite.PassedTests}, Failed: {suite.FailedTests}, Pass Rate: {suite.PassRate:F2}%");
        }
        Console.WriteLine();
        Console.WriteLine($"Total tests: {runResult.TotalTests}");
        Console.WriteLine($"Passed: {runResult.PassedTests}");
        Console.WriteLine($"Failed: {runResult.FailedTests}");
        Console.WriteLine($"Pass rate: {runResult.PassRate:F2}%");
        Console.WriteLine($"Results saved to: {filePath}");
    }
}
