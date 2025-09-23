# Translator Teacher

## Configuration Options

### appsettings.json
Options with samples.
```json
{
  "WhisperApiUrl": "http://localhost:9000",
  "NpcApiUrl": "http://localhost:8082",
  "TranslatorApiUrl": "http://localhost:8081",
  "NpcPrompt": "/no_think\nYou are an NPC.\nYou are responding to the user's message in the first person.\nYour response should be in english. Response with simple statements and questions. Respond in a way that continues the conversation."
}
```

## Endpoints

* http://localhost:9000 - Whisper - Speech to Text
* http://localhost:8082 - Qwen 3 4B - LLM - Acts as the NPC
* http://localhost:8081 - Hunyuan MT Chimera 7B - LLM - Translations
* http://localhost:8000/docs - Parler TTS - Text to Speech