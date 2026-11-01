# Sophia language, voice, and business answers

Sophia is the existing BusinessOS assistant (`sophia` agent). Chat still goes through `AgentEmployeeService` and `AgentRuntimeOrchestrator`. This note covers the language and voice behavior added on that path.

## Languages

Supported reply languages:

| Code | Meaning | Direction |
| --- | --- | --- |
| `auto` | Detect each message | Follows the detected language |
| `en` | English | Left to right |
| `ur` | Urdu script | Right to left |
| `ur-Latn` | Roman Urdu | Left to right |
| `ar` | Arabic | Right to left |

Detection lives in `ConversationLanguage`. It uses the current message first, then an explicit switch ("explain this in Urdu", "بالعربية", "in English"), then the language stored on the conversation. A short follow-up such as "Why?" keeps the previous language. Choosing English, Urdu, or Arabic in the chat keeps that language until the user asks to switch. `auto` is the default.

Roman Urdu is not treated as English. Product names, SKUs, and customer names are left as stored; the system prompt tells the model not to translate them.

`ConversationLanguage.ExpandForRouting` appends English words such as `sales` and `this month` so the existing tool matchers call the same database tools for Urdu, Roman Urdu, and Arabic. The original message is what the model sees. Transaction numbers still come from tenant-scoped queries, not from the vector store.

The chosen language is saved on the conversation memory (`PreferredLanguage`) when a turn completes.

## Voice

Voice is turn-based. The browser records one utterance (Chrome speech recognition, `en-US`, `ur-PK`, or `ar-SA` when the browser supports it), the server answers, then speech is synthesized. This is not a full-duplex realtime call.

Speech providers, in order:

1. OpenAI `gpt-4o-mini-tts` when `Ai:OpenAiApiKey` is set. The voice catalog (Coral by default) speaks the reply language from the text plus a pronunciation instruction.
2. Edge neural voices when OpenAI speech is unavailable: Jenny/Aria/Guy for English, Uzma/Asad for Urdu, Zariyah/Hamed for Arabic.

Spoken replies are AI-generated. The chat says so. Users can turn auto-speak off, pick a voice, change the pace, stop playback, and replay the last reply.

Browser recognition and browser-voice fallback depend on the installed voices. Urdu and Arabic recognition need a browser that advertises `ur-PK` and `ar-SA`.

## Models and configuration

From `appsettings.json`:

- Chat model: `Ai:OpenAiModel` (`gpt-4o-mini`)
- Embeddings: `Ai:OpenAiEmbeddingModel` (`text-embedding-3-small`)
- Speech model: `Ai:Tts:OpenAiModel` (`gpt-4o-mini-tts`)
- Default voice: `Ai:Tts:DefaultVoice` (`coral`)
- Qdrant collection: `Qdrant:CollectionName`

Leave `Ai:OpenAiApiKey` empty to keep paid calls off. Do not commit keys. Local secrets belong in `deploy/env/.env.dev`, not in source.

Qdrant search stays tenant-filtered. If Qdrant is down, readiness is degraded and database-backed tools still answer.

## Tests

`ConversationLanguageTests` covers English, Urdu script, Roman Urdu, Arabic, language lock, follow-up retention, and the sales/low-stock tool choice. The Angular spec `chat-direction.spec.ts` covers RTL versus LTR.

Real OpenAI transcription, TTS audio, and a live Qdrant collection are not part of the unit tests.
