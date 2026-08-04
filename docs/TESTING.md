# Wasta AI features — acceptance checklist

Covers the AI Career Coach and the Support Chatbot.

**Sign off only when every item is verified. Flag anything uncertain rather than assuming pass.**

Status keys used below:
- **[auto]** — covered by the automated suite; `dotnet test` proves it
- **[verified]** — checked by hand against a running app
- **[blocked]** — cannot be verified yet, and why

> **The standing rule:** mocked-provider runs prove the plumbing, never the model. Rows marked
> *needs a real key* are false-green under mocks — a stub returns whatever string it was told to,
> so it cannot tell you whether Groq or Gemini would leak a percentage or fall for an injection.
> Re-run those periodically, since provider-side model updates change behaviour silently.

---

## Setup

- [auto] `dotnet build WastaCareerCoach.slnx` clean — 0 warnings, 0 errors (CI enforces `--warnaserror`)
- [auto] `dotnet test WastaCareerCoach.slnx` — 84 passing (50 Career Coach, 34 Support Chat)
- [auto] `npx tsc --noEmit` clean in `src/frontend/coach-card` and `src/frontend/chat-widget`
- [verified] Test doubles live under `tests/`. One deliberate exception: `NullJobListingProvider`
  ships in `src/` as a production null-object default so the chatbot runs before the jobs
  integration exists.

## AI Career Coach — functional

- [verified] Submit returns in **~140ms**, far under the 3s budget, with the row left `Pending`
- [auto][verified] `StudentCoachPlan` row is `Pending` before generation finishes
- [verified] Card shows all four pieces: assessment, 4-week plan (weeks 1–4, 2–3 actions each),
  project suggestion, interview line
- [blocked] Score/percentile/static feedback render independently — *needs the real results page;
  the dev host has no static-feedback blurbs*
- [auto] `POST /api/admin/coach-plans/{attemptId}/regenerate` resets `AttemptCount`, re-enqueues,
  writes an audit row

## AI Career Coach — guardrails

- [auto] Validator rejects a numeric percentage, `percentile`, `41 percent`, `forty-one percent`,
  `41 out of 100`, `41/100`
- [auto] Validator rejects `hire`/`hired`/`hiring`/`salary`/`job offer`/`you will get`
- [auto] Validator does **not** false-reject legitimate text (`Hampshire`, `higher-order functions`,
  `score each model and compare 3 runs`)
- [auto] Outbound `student_context` carries no name, email, university, city, or CV — the DTO has no
  fields for them, so it is structurally impossible
- [auto] A prompt-injection string in `skills` does not change the output shape or land in the
  stored plan
- [blocked] **A real model obeys all of the above** — *needs a real key*

## AI Career Coach — failure modes

- [auto] Groq 429 → Gemini serves. Groq 400 → Gemini **not** tried (non-retryable)
- [auto] Both providers down → `Failed`, `AttemptCount` increments, results page unaffected
- [auto] Malformed response → exactly one retry, then `Failed`
- [verified] `Ai:Enabled = false` → every plan `Skipped`, endpoint `unavailable`, **zero** errors logged
- [auto] Sweeper retries `Failed` plans under the attempt cap, and rescues plans abandoned in
  `Pending` by a full queue or a restart
- [auto] Sweeper leaves recent `Pending` and all `Ready` plans alone

## Support Chatbot — functional

- [verified] Anonymous visitor chats with no auth wall; a logged-in student's id attaches to the session
- [auto] Unknown session: `GET messages` → empty list (never an error); `POST messages` → 404
- [verified] Anonymous session creation without a `visitorId` is refused (400) — it would be unreachable
- [blocked] Page reload keeps the conversation — *needs the React widget mounted in a real app*

## Support Chatbot — cross-visit memory *(treat as a privacy surface)*

- [auto] Returning student's new session is seeded with context from earlier sessions
- [auto] **Student A's history never reaches Student B's session**
- [auto] Anonymous history never carries across sessions, even reusing the same `visitorId`
- [auto] `CrossSessionMemoryTurns` bounds how much is pulled — no unbounded growth

## Support Chatbot — session authorization

- [auto][verified] A stolen session id alone leaks nothing: history returns empty, send returns 404
- [auto][verified] A student's session cannot be read or continued by another student
- [verified] The rightful owner is unaffected
- [auto] Unauthorized reports **404, not 403**, so the API cannot be used to enumerate session ids

## Support Chatbot — job recommendations

- [auto][verified] Listings match the provider's output verbatim (title, employer, URL)
- [auto] No `OPEN_OPPORTUNITIES` block in the prompt when there are no listings
- [auto][verified] The provider receives the correct `studentId` (or null) — personalization varies
  by identity
- [blocked] **A real model only raises jobs when relevant, and never invents a listing or URL** —
  *needs a real key*

## Support Chatbot — abuse guardrails

- [auto] Over-length, too-fast, and past-cap messages are all rejected with **no AI call**
- [verified] Per-IP rate limits return 429 on session creation and messages
- [auto] The user's message is a separate chat turn, never spliced into the system prompt
- [auto][verified] Unresolved `[TODO:]` drafts and editor notes are stripped before the model sees
  the knowledge base; a startup warning counts what remains
- [auto] Both providers down mid-chat → friendly fallback, user's message still saved, no exception
  reaches the client
- [blocked] **A real model declines account questions and refuses injection** — *needs a real key*

## Cross-cutting

- [verified] 360px mobile: no horizontal scroll (measured), nothing clipped
- [verified] Dark mode: bubbles, plan, and disclaimer all legible
- [auto] No secrets committed — CI fails on tracked `.env`/`appsettings.Development|Local|Production`
  files or committed key patterns
- [verified] `Ai:Enabled = false` disables both features from one flag

---

## How to run the blocked rows

```bash
dotnet user-secrets --project src/Wasta.DevHost set "Ai:Providers:groq:ApiKey" "<key>"
dotnet user-secrets --project src/Wasta.DevHost set "Ai:Providers:groq:Model" "<model-id>"
dotnet run --project src/Wasta.DevHost
```

The provider chain is `[groq, gemini, dev]` and skips unconfigured providers, so a real key takes
over automatically and the fixture provider is never reached.

## Known gaps before launch

1. **The knowledge base has 9 unresolved TODOs.** The chatbot cannot answer account, retake,
   unlock, or privacy-policy questions until a product owner fills them in. The app warns about
   this on every boot.
2. **No production host.** `Wasta.DevHost` is a harness and refuses to start outside Development.
   A real host needs Postgres, both migrations, real authentication, and real implementations of
   the five ports.
3. **No real-provider run yet.** Every row above marked *needs a real key* is genuinely unverified.
