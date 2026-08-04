# Wasta.DevHost

A runnable harness for the Career Coach and Support Chat modules. **Development only** — it fakes
authentication from request headers and refuses to start outside the Development environment.

## Run it

```bash
dotnet run --project src/Wasta.DevHost
```

Then open the URL it prints. No database and no API keys are needed: EF runs in memory, and
`DevEchoProvider` returns fixed fixtures so the whole flow completes.

## Adding real AI keys

```bash
dotnet user-secrets --project src/Wasta.DevHost set "Ai:Providers:groq:ApiKey" "<key>"
dotnet user-secrets --project src/Wasta.DevHost set "Ai:Providers:groq:Model" "<model-id>"
```

The chain is `[groq, gemini, dev]` and skips unconfigured providers, so a real key takes over
automatically and `dev` is never reached. This is how you run the guardrail rows of the QA
checklist against an actual model — the fixtures cannot tell you what Groq or Gemini would say.

## Faking identity

| Header | Effect |
|---|---|
| `X-Dev-Student-Id: 1` | authenticated as student 1 |
| `X-Dev-Admin: true` | adds the admin role |
| neither | anonymous visitor |
| `X-Wasta-Visitor-Id: <id>` | proves ownership of an anonymous chat session |

Two students are seeded: **1** (Data & AI) and **2** (Software Engineering). Switching between them
is how you verify that one student never sees the other's chat memory or coach plan.

## What the demo page covers

1. **Submit an assessment** — reports the response time, which should stay far under the 3s budget
   because generation is queued rather than awaited.
2. **Coach plan** — polls the real endpoint and renders the plan, same states as the shipping card.
3. **Support chat** — real sessions, real job recommendations, real per-IP rate limits.
4. **Authorization probe** — replays the session id under a different identity and asserts nothing
   leaks.

## What this is not

- Not the production UI. The shipping components are the React ones in `src/frontend/`.
- Not a substitute for the real port implementations. Everything in `Adapters/` is demo data
  standing in for your assessment, profile, audit, and job-listing sources.
- Not deployable. It is a test rig, and `Program.cs` enforces that.

## Moving to the real thing

Replace the in-memory EF registrations in `Program.cs` with `UseNpgsql(connectionString)`, run both
modules' migrations, implement the five ports against your real data, and swap `DevAuthHandler` for
your actual authentication.
