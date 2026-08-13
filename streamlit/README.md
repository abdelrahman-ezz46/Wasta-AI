# Streamlit preview

A quick visual way to show people what the AI features look like — useful for demos and
stakeholder reviews.

It is **not a mockup**. It calls the same endpoints the production React components call, so every
card on screen is rendered from a real API response. If the backend changes, this changes with it.

## Run

Two processes: the .NET backend, then this.

```bash
# terminal 1 — the API
dotnet run --project src/Wasta.DevHost

# terminal 2 — the preview
pip install -r streamlit/requirements.txt
streamlit run streamlit/app.py
```

Opens at http://localhost:8501. If the backend isn't up, the page says so and tells you what to run
rather than showing an empty screen.

Point it elsewhere with `WASTA_API`:

```bash
WASTA_API=https://staging.example.com streamlit run streamlit/app.py
```

## What to show in a demo

**Results page tab**
- Submit an assessment. Note the response time shown — the page never waits on the AI.
- The score and section breakdown appear instantly; the coach plan fills in underneath.
- All four pieces of the plan render: the read on where they stand, the 4-week plan, a project
  suggestion, and the interview line.

**Support chat tab**
- *"what jobs are open?"* — listings come from the host app, personalized per student.
- *"what is my score?"* — it declines. The chatbot has no access to account data and says so
  instead of guessing.
- Switch student in the sidebar and reset the session: one student never sees another's history.

## Caveats

- Without an AI key, replies come from the `dev` fixture provider and are clearly prefixed
  `[dev fixture]`. Set a real key on the backend for genuine model output — see the root README.
- The seeded students and job listings are demo data from `Wasta.DevHost/Adapters/`, not real
  records.
- This is a preview tool. The shipping UI is the React components in `src/frontend/`.
