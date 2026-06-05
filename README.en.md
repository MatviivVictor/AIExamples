# AIAgentChat

AIAgentChat is a console application for interacting with AI models, built on .NET 9. The project demonstrates modern approaches to AI agent development, including RAG (Retrieval-Augmented Generation), structured output, and security mechanisms (Guardrails).

## Core Features

- **Provider-agnostic**: Support for Ollama, OpenAI, and Gemini via `Microsoft.Extensions.AI`.
- **RAG**: Answering questions based on local documentation (`Manuals/user-guid.md`).
- **Structured Output**: `classify` command for categorizing requests in JSON format.
- **Guardrails**: Validation of input data, output data, and RAG context for safety.
- **Evaluation**: Built-in tools for assessing response quality and guardrails performance.
- **Logging**: Structured event logging to the console.
- **Caching**: In-memory caching of RAG results and classifications for better performance.
- **Docker Support**: Easy deployment and execution in a container.

## Requirements

- .NET 9 SDK
- Docker (optional)
- Ollama (for local models)
- OpenAI API Key (optional)
- Gemini API Key (optional)

## Setup

### Ollama (Local)
1. Install [Ollama](https://ollama.com/).
2. Start the service: `ollama serve`.
3. Download the models:
   ```bash
   ollama pull llama3
   ollama pull nomic-embed-text
   ```

### OpenAI
Set the environment variable:
```bash
export OPENAI_API_KEY="your-key"
```

### Gemini
Set the environment variable:
```bash
export GEMINI_API_KEY="your-key"
```

## Configuration

The project uses several settings files:
- `appsettings.json`: General settings and model list.
- `appsettings.ollama.json`: Settings for local Ollama.
- `appsettings.docker.ollama.json`: Settings for Ollama when running in Docker.
- `appsettings.openai.json`: Settings for OpenAI.
- `appsettings.gemini.json`: Settings for Gemini.

## How to Run Locally

```bash
cd AIAgentChat
dotnet restore
dotnet run
```

Upon startup, select a model from the list.

## How to Run with Docker

### Build
```bash
docker build -t aiagentchat -f AIAgentChat/Dockerfile .
```

### Run (Interactive)
```bash
docker run -it --rm \
  -e OPENAI_API_KEY="$OPENAI_API_KEY" \
  -e GEMINI_API_KEY="$GEMINI_API_KEY" \
  --add-host=host.docker.internal:host-gateway \
  aiagentchat
```

### Docker Compose
```bash
docker compose run --rm aiagentchat
```

**Note for Linux**: If you are using Ollama on the host, ensure that Ollama listens on all interfaces (OLLAMA_HOST=0.0.0.0) or use `--add-host=host.docker.internal:host-gateway`.

## Chat Commands

- `exit`: Quit the application.
- `classify`: Enter text classification mode (Structured Output).
- `docs`: Ask a question based on local documentation (RAG).
- `eval`: Open the evaluation menu (Evaluation Suites).

## Logging and Caching

### Logging
- Uses `Microsoft.Extensions.Logging.Console`.
- Logs events: startup, model selection, requests, cache hit/miss, guardrail blocks, provider errors (429, etc.).
- **Security**: Secrets and full prompt texts are not logged. Previews or hashes are used for text data.

### Caching
- Uses `IMemoryCache`.
- Cached items: documentation search results (30 min), final RAG answers (15 min), classification results (30 min).
- Cache can be disabled in `appsettings.json`: `"Cache": { "Enabled": false }`.

## Troubleshooting

- **OPENAI_API_KEY is not set**: Check your environment variables.
- **Ollama 404 (embedding)**: Ensure `nomic-embed-text` is downloaded (`ollama pull`).
- **Docker cannot connect to Ollama**: Use the "Ollama Docker - llama3" model from the list or check `host.docker.internal` settings.
- **429 Too Many Requests**: API quota exhausted or rate limits exceeded.

## Security

- API keys are passed only via environment variables.
- Filtering (Guardrails) is implemented to prevent Prompt Injection and leakage of unwanted information.
- RAG context is treated as untrusted data.

## Learning Roadmap
1. Basic LLM integration
2. Provider-agnostic architecture
3. Structured outputs
4. RAG
5. Guardrails
6. Evaluation
7. **Production readiness** (Logging, Caching, Docker) - *Current Stage*
