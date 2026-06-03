using System.Text.Json;
using AIAgentChat.Application.Models.Evaluation;

namespace AIAgentChat.Application.Services.Evaluation;

public class EvaluationResultWriter
{
    public string SaveResult(EvaluationRunResult runResult)
    {
        var resultsDir = Path.Combine(AppContext.BaseDirectory, "Evaluation", "Results");
        
        if (!Directory.Exists(resultsDir))
        {
            Directory.CreateDirectory(resultsDir);
        }

        var fileName = $"eval-results-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(resultsDir, fileName);

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        var json = JsonSerializer.Serialize(runResult, options);
        File.WriteAllText(filePath, json);

        return filePath;
    }
}
