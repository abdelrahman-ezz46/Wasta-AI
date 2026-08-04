# Wasta platform knowledge base (draft v1)

This file is the chatbot's entire source of truth about how the platform
works. It gets injected into the system prompt in full - there is no search
or retrieval step. Keep it accurate, keep it current, and keep entries
short: everything here counts against the model's context on every message.

This draft only contains what was established while building the AI Career
Coach feature. A real product/support owner should review and expand it -
especially the TODO markers - before this goes live. Do not let the model
guess past what's written here; that's what the system prompt's "don't
know" rule is for.

## What Wasta is

Wasta is a skills-assessment platform. Students complete an assessment,
receive a Wasta Score, and get discovered by employers searching the
platform. [TODO: one or two sentences on who Wasta is for and what problem
it solves - written for a support bot, not a pitch deck.]

## The Wasta Score

- Students take a skills assessment for a specific track (for example,
  "Data & AI"). [TODO: list the actual tracks offered.]
- The results page shows an overall score, a percentile, and a breakdown by
  section (for example, in a Data & AI track: Python & data handling,
  Statistics & ML fundamentals, Applied modelling, SQL & data pipelines).
- Every section also gets short written feedback that appears instantly -
  this is fixed, pre-written content, not AI-generated, and it is the same
  for every student who lands in that section's score band.
- The Wasta Score is deterministic and rule-based: the same answers always
  produce the same score, and the scoring method is published. It is not
  influenced by the AI Career Coach in any way.
- [TODO: retake policy - can a student retake the assessment, how often,
  does a retake replace or add to their record?]

## The AI Career Coach

- A personalized study plan that appears below the score on the results
  page, generated once right after a student submits their assessment.
- It contains: a short written summary of how the student's sections relate
  to each other, a 4-week study plan with concrete actions per week, one
  suggested project sized to close their biggest gap, and one sentence they
  could use in an interview about their weakest area.
- It is study advice, not a hiring signal. It never states or implies
  anything about a student's chances of being hired, their salary, or
  whether any company would hire them.
- It can take up to about a minute to appear after submission. If it never
  appears, the rest of the results page (score, percentile, section
  feedback) is unaffected - the coach is an optional add-on, not a
  dependency.
- Support can ask an administrator to regenerate a student's plan if it
  failed to generate. [TODO: how does a student actually request this today
  - a support ticket, a button, something else?]

## Employers and being discovered

- Wasta's model centers on students being found by employers who search
  the platform (an "unlock"). [TODO: what actually happens when an employer
  "unlocks" a profile - what does the employer see, does the student get
  notified, is there a cost involved for either side?]
- [TODO: how does a student control their visibility to employers - is
  there an opt-in/opt-out, a minimum score threshold, anything else?]

## Privacy and data

- Only the minimum needed data is ever sent to the AI models that generate
  the Career Coach: the student's track, their section scores, and - if
  provided - a short list of skills, project titles, and graduation year.
  Name, email, university, city, and CV are never sent.
- [TODO: point to the actual privacy policy / PDPL disclosure URL once one
  exists, so the bot can link to it instead of describing it secondhand.]

## Accounts and getting help

- [TODO: how does someone create an account, reset a password, delete their
  account, or reach a human for something this bot can't answer? This
  section is the one the bot will lean on most for "I don't know, but
  here's how to get a real answer" - fill it in first.]
