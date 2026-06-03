using System.Text.Json;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class EvaluationDatasetLoader
{
    public List<RagEvaluationCase> LoadRagCases()
    {
        return LoadCases<RagEvaluationCase>("rag-eval-cases.json");
    }

    public List<ClassifyEvaluationCase> LoadClassifyCases()
    {
        return LoadCases<ClassifyEvaluationCase>("classify-eval-cases.json");
    }

    public List<GuardrailsEvaluationCase> LoadGuardrailsCases()
    {
        return LoadCases<GuardrailsEvaluationCase>("guardrails-eval-cases.json");
    }

    private List<T> LoadCases<T>(string fileName)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Evaluation", "Datasets", fileName);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[Warning] Dataset file not found: {filePath}");
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<T>>(json, options) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to load dataset {fileName}: {ex.Message}");
            return [];
        }
    }
}
