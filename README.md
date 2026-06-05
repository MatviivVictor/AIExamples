# AIAgentChat

AIAgentChat — це консольний застосунок для спілкування з AI-моделями, побудований на .NET 9. Проєкт демонструє сучасні підходи до розробки AI-агентів, включаючи RAG (Retrieval-Augmented Generation), структурований вивід та захисні механізми (Guardrails).

## Основні можливості

- **Provider-agnostic**: підтримка Ollama, OpenAI та Gemini через `Microsoft.Extensions.AI`.
- **RAG**: відповіді на питання на основі локальної документації (`Manuals/user-guid.md`).
- **Structured Output**: команда `classify` для класифікації запитів у форматі JSON.
- **Guardrails**: перевірка вхідних даних, вихідних даних та контексту RAG на безпеку.
- **Evaluation**: вбудовані інструменти для оцінки якості відповідей та роботи guardrails.
- **Logging**: структуроване логування подій у консоль.
- **Caching**: in-memory кешування результатів RAG та класифікації для швидкодії.
- **Docker Support**: легкий запуск у контейнері.

## Вимоги

- .NET 9 SDK
- Docker (опційно)
- Ollama (для локальних моделей)
- OpenAI API Key (опційно)
- Gemini API Key (опційно)

## Налаштування

### Ollama (Локально)
1. Встановіть [Ollama](https://ollama.com/).
2. Запустіть сервіс: `ollama serve`.
3. Завантажте моделі:
   ```bash
   ollama pull llama3
   ollama pull nomic-embed-text
   ```

### OpenAI
Встановіть змінну оточення:
```bash
export OPENAI_API_KEY="ваш-ключ"
```

### Gemini
Встановіть змінну оточення:
```bash
export GEMINI_API_KEY="ваш-ключ"
```

## Конфігурація

Проєкт використовує декілька файлів налаштувань:
- `appsettings.json`: загальні налаштування та список моделей.
- `appsettings.ollama.json`: налаштування для локального Ollama.
- `appsettings.docker.ollama.json`: налаштування для Ollama при запуску в Docker.
- `appsettings.openai.json`: налаштування для OpenAI.
- `appsettings.gemini.json`: налаштування для Gemini.

## Як запустити локально

```bash
cd AIAgentChat
dotnet restore
dotnet run
```

При старті виберіть модель зі списку.

## Як запустити в Docker

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

**Примітка для Linux**: Якщо ви використовуєте Ollama на хості, переконайтеся, що Ollama слухає на всіх інтерфейсах (OLLAMA_HOST=0.0.0.0) або використовуйте `--add-host=host.docker.internal:host-gateway`.

## Команди в чаті

- `exit`: завершити роботу.
- `classify`: перейти в режим класифікації тексту (Structured Output).
- `docs`: поставити питання по локальній документації (RAG).
- `eval`: відкрити меню оцінки (Evaluation Suites).

## Логування та Кешування

### Логування
- Використовується `Microsoft.Extensions.Logging.Console`.
- Логуються події: старт, вибір моделі, запити, cache hit/miss, блокування guardrails, помилки провайдерів (429 та інші).
- **Безпека**: Секрети та повні тексти промптів не логуються. Для тексту використовується прев'ю або хеш.

### Кешування
- Використовується `IMemoryCache`.
- Кешуються: результати пошуку в документації (30 хв), фінальні RAG-відповіді (15 хв), результати класифікації (30 хв).
- Кеш можна вимкнути в `appsettings.json`: `"Cache": { "Enabled": false }`.

## Troubleshooting

- **OPENAI_API_KEY is not set**: Перевірте змінні оточення.
- **Ollama 404 (embedding)**: Переконайтеся, що `nomic-embed-text` завантажено (`ollama pull`).
- **Docker cannot connect to Ollama**: Використовуйте модель "Ollama Docker - llama3" у списку або перевірте налаштування `host.docker.internal`.
- **429 Too Many Requests**: Вичерпано квоту API або перевищено ліміти частоти запитів.

## Безпека

- API ключі передаються тільки через змінні оточення.
- Додано фільтрацію (Guardrails) для запобігання Prompt Injection та витоку небажаної інформації.
- Контекст RAG розглядається як неперевірені дані (untrusted data).

## Етапи розвитку (Learning Roadmap)
1. Basic LLM integration
2. Provider-agnostic architecture
3. Structured outputs
4. RAG
5. Guardrails
6. Evaluation
7. **Production readiness** (Логування, Кешування, Docker) - *Поточний етап*
