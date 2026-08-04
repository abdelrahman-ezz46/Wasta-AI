# Closing the chatbot's knowledge gaps

The support chatbot answers only from
[`PlatformKnowledge.v1.md`](../src/Wasta.SupportChat/Knowledge/PlatformKnowledge.v1.md). It has no
search, no access to your database, and no ability to guess — anything not written there gets an
honest "I don't have that information, here's how to reach support."

There are currently **9 unresolved gaps**. The app logs a warning counting them on every startup.

This document turns each one into a specific question. Answer them in plain sentences, paste the
answers into the knowledge file, and the gap closes. **Do not answer a question you aren't sure
about** — leave the `[TODO:]` marker in place. A stripped gap is safe; a confidently wrong answer
reaches students.

---

## How to write answers

The whole file is pasted into the prompt on every message, so length costs money and latency on
every single conversation. Aim for a few short sentences per topic.

**Good** — specific, bounded, answers the question a student actually asks:
> Students can retake an assessment once every 30 days. The most recent attempt is the one
> employers see; earlier attempts are kept but not shown.

**Bad** — vague, marketing-toned, or unbounded:
> Wasta offers flexible retake options designed to help every student put their best foot forward
> on their journey to career success.

Two conventions in that file:
- `<!-- HTML comments -->` are notes to whoever edits it and never reach the model. Write freely.
- `[TODO: ...]` marks a known gap and is stripped before the model sees it.

---

## The questions

### 1. What is Wasta, in two sentences?
For a student who landed on the site and has no idea what it is.

### 2. What tracks can a student be assessed in?
Just the list. "Data & AI" is the only one currently named anywhere in the code.

### 3. Can a student retake an assessment?
- How often, or is there a cooldown?
- Does a retake replace the previous score, or are both kept?
- Which one do employers see?

### 4. How does a student request a coach-plan regeneration?
The admin endpoint exists, but nothing documents how a student *asks* for it. Is there a support
ticket, a button, an email address? If the honest answer is "contact support", say that.

### 5. What actually happens when an employer unlocks a profile?
- What does the employer see that they couldn't before?
- Is the student notified?
- Does it cost the student anything? (The chatbot must never speculate about money.)

### 6. How does a student control their visibility to employers?
- Is there an opt-in or opt-out?
- Is there a minimum score to appear in employer searches?
- Can they hide specific attempts?

### 7. Where is the privacy policy?
A URL. The knowledge base currently describes your data handling second-hand, which is exactly the
kind of thing that should link to the authoritative document instead.

### 8. Account basics
- How does someone create an account?
- How do they reset a password?
- How do they delete their account and data?

### 9. How does someone reach a human?
**Fill this one in first.** It's the fallback for every question the bot can't answer, so until it
exists the bot's "contact support" advice is a dead end. An email address or a help-desk URL is
enough.

---

## After you've answered

1. Paste the answers into the knowledge file, replacing the matching `[TODO:]` markers.
2. Run `dotnet run --project src/Wasta.DevHost` and check the startup log — the warning count should
   drop.
3. Ask the chatbot each question through the demo page and confirm the answer matches what you
   wrote. The file is re-read when its timestamp changes, so no redeploy is needed.

## What is deliberately *not* here

Don't add anything that varies per student — scores, application status, unlock history. The
chatbot has no access to individual account data by design, and the prompt instructs it to say so.
Writing account-specific claims into the knowledge base would push it into guessing about people.
