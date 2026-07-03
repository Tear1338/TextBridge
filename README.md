# TextBridge

real-time AI translation for Habbo Hotel chat via G-Earth extension.

- translate incoming/outgoing messages (chat, shout, whisper)
- Works with: LocalLLM, Grok, OpenAI, Claude, Gemini, FantasyAI

## FantasyAI Setup

FantasyAI (https://www.fantasyai.cloud) gives access to 30+ models (Claude, GPT, Gemini, DeepSeek, Llama, Grok) with a single API key:
- create an API key from the FantasyAI dashboard
- select the FantasyAI provider in TextBridge and paste your key
- use the refresh button next to the model dropdown to load the full model list

## Requirements
- .NET 8.0 Runtime
- G-Earth
- API key for chosen provider (or LocalLLM with Ollama)

## LocalLLM Setup

for LocalLLM: Ollama must be running in the background:
- download Ollama from https://ollama.ai
- run `ollama serve` before using TextBridge
- default: localhost:11434

- recommended Model for Ollama `gemma3:12b`
