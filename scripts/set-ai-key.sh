#!/usr/bin/env bash
# Interactively store an AI provider key for the dev host.
#
# The key is read from a hidden prompt, so it is never typed as a command
# argument. That keeps it out of your shell history, out of the process
# list, and out of any transcript. It is stored by `dotnet user-secrets`,
# which writes to your user profile OUTSIDE this repository, so it cannot be
# committed by accident.
#
#   ./scripts/set-ai-key.sh              # groq (default)
#   ./scripts/set-ai-key.sh gemini
set -euo pipefail

cd "$(dirname "$0")/.."

PROVIDER="${1:-groq}"
PROJECT="src/Wasta.DevHost"

case "$PROVIDER" in
  groq)
    CONSOLE="https://console.groq.com/keys"
    DEFAULT_MODEL="llama-3.3-70b-versatile"
    CHAT_MODEL="llama-3.1-8b-instant"
    ;;
  gemini)
    CONSOLE="https://aistudio.google.com/apikey"
    DEFAULT_MODEL=""
    CHAT_MODEL=""
    ;;
  *)
    echo "Unknown provider '$PROVIDER'. Use: groq | gemini" >&2
    exit 1
    ;;
esac

echo "Setting up the '$PROVIDER' provider for $PROJECT."
echo "Get a key from: $CONSOLE"
echo

printf 'Paste your %s API key (input hidden): ' "$PROVIDER"
read -rs API_KEY
echo
if [ -z "$API_KEY" ]; then
  echo "No key entered. Nothing changed." >&2
  exit 1
fi

read -rp "Model ID [${DEFAULT_MODEL:-required}]: " MODEL
MODEL="${MODEL:-$DEFAULT_MODEL}"
if [ -z "$MODEL" ]; then
  echo "A model ID is required. Check $CONSOLE for current IDs." >&2
  exit 1
fi

dotnet user-secrets --project "$PROJECT" set "Ai:Providers:$PROVIDER:ApiKey" "$API_KEY" >/dev/null
dotnet user-secrets --project "$PROJECT" set "Ai:Providers:$PROVIDER:Model" "$MODEL" >/dev/null
unset API_KEY

# Chat is the high-volume path and only needs short answers, so point it at
# the smaller model where one exists. See the model table in README.md.
if [ -n "$CHAT_MODEL" ]; then
  read -rp "Use '$CHAT_MODEL' for support chat? [Y/n]: " USE_CHAT
  if [ "${USE_CHAT:-y}" != "n" ] && [ "${USE_CHAT:-y}" != "N" ]; then
    dotnet user-secrets --project "$PROJECT" set "SupportChat:Model" "$CHAT_MODEL" >/dev/null
    echo "  support chat -> $CHAT_MODEL"
  fi
fi

echo
echo "Stored. Keys live outside the repo and cannot be committed."
echo "Now RESTART the dev host - secrets are read once at startup:"
echo "    dotnet run --project $PROJECT"
echo
echo "Then confirm a real provider is serving (should say '$PROVIDER', not 'dev'):"
echo "    curl -s -H 'X-Dev-Admin: true' http://localhost:5219/api/admin/coach-plans/stats"
