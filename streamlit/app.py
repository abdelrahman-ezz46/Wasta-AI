"""
Wasta AI — visual preview.

A stakeholder-facing way to see what the AI Career Coach and Support Chatbot
look like. It is a thin front end over the real .NET endpoints in
Wasta.DevHost, not a mockup: every card on screen is rendered from an actual
API response, so what you see is what the feature really produces.

Run:
    dotnet run --project src/Wasta.DevHost     # terminal 1
    streamlit run streamlit/app.py             # terminal 2
"""

import os
import time
import uuid

import requests
import streamlit as st

API = os.environ.get("WASTA_API", "http://localhost:5219")
TIMEOUT = 30

st.set_page_config(page_title="Wasta AI preview", page_icon="🎓", layout="centered")


# --------------------------------------------------------------------------
# API helpers
# --------------------------------------------------------------------------
def auth_headers(student_id):
    return {"X-Dev-Student-Id": str(student_id)} if student_id else {}


def chat_headers(student_id):
    return {**auth_headers(student_id), "X-Wasta-Visitor-Id": st.session_state.visitor_id}


def backend_is_up():
    try:
        return requests.get(f"{API}/api/dev/health", timeout=3).ok
    except requests.RequestException:
        return False


def submit_assessment(student_id, sections):
    return requests.post(
        f"{API}/api/dev/assessments/submit",
        json={"studentId": student_id, "sections": sections},
        headers=auth_headers(student_id),
        timeout=TIMEOUT,
    ).json()


def fetch_plan(student_id):
    return requests.get(
        f"{API}/api/students/me/coach-plan", headers=auth_headers(student_id), timeout=TIMEOUT
    ).json()


def ensure_session(student_id):
    if st.session_state.get("chat_session"):
        return st.session_state.chat_session
    r = requests.post(
        f"{API}/api/chat/sessions",
        json={"visitorId": st.session_state.visitor_id},
        headers=auth_headers(student_id),
        timeout=TIMEOUT,
    )
    if not r.ok:
        return None
    st.session_state.chat_session = r.json()["sessionId"]
    return st.session_state.chat_session


def send_chat(student_id, text):
    session_id = ensure_session(student_id)
    if not session_id:
        return {"outcome": "error", "reply": "Could not start a chat session (rate limited?)."}
    r = requests.post(
        f"{API}/api/chat/sessions/{session_id}/messages",
        json={"message": text},
        headers=chat_headers(student_id),
        timeout=TIMEOUT,
    )
    if r.status_code == 429:
        return {"outcome": "rate_limited", "reply": "Too many messages — the per-IP rate limit kicked in."}
    if not r.ok:
        return {"outcome": "error", "reply": f"Request failed ({r.status_code})."}
    return r.json()


# --------------------------------------------------------------------------
# State
# --------------------------------------------------------------------------
st.session_state.setdefault("visitor_id", str(uuid.uuid4()))
st.session_state.setdefault("chat_session", None)
st.session_state.setdefault("messages", [])
st.session_state.setdefault("submitted", False)

STUDENTS = {"Student 1 — Data & AI": 1, "Student 2 — Software Engineering": 2, "Anonymous visitor": None}

SECTIONS = [
    ("Python & data handling", 78),
    ("Statistics & ML fundamentals", 41),
    ("Applied modelling", 55),
    ("SQL & data pipelines", 34),
]

# --------------------------------------------------------------------------
# Sidebar
# --------------------------------------------------------------------------
with st.sidebar:
    st.header("Preview controls")
    who = st.selectbox("Signed in as", list(STUDENTS), index=0)
    student_id = STUDENTS[who]

    st.caption(
        "Switch between students to see that one student never sees another's "
        "coach plan or chat history."
    )

    if st.button("Reset chat session"):
        st.session_state.chat_session = None
        st.session_state.messages = []
        st.rerun()

    st.divider()
    st.caption(f"Backend: `{API}`")
    if backend_is_up():
        st.success("Backend connected")
    else:
        st.error("Backend not running")

st.title("Wasta AI")
st.caption("Live preview of the AI Career Coach and Support Chatbot, rendered from the real API.")

if not backend_is_up():
    st.error("The backend isn't running, so there's nothing to preview yet.")
    st.code("dotnet run --project src/Wasta.DevHost", language="bash")
    st.caption(f"Expected at {API}. Set WASTA_API to point somewhere else.")
    st.stop()

coach_tab, chat_tab = st.tabs(["Results page", "Support chat"])

# --------------------------------------------------------------------------
# Career Coach
# --------------------------------------------------------------------------
with coach_tab:
    if student_id is None:
        st.info("Pick a student in the sidebar — anonymous visitors don't have an assessment.")
    else:
        st.subheader("1. Take the assessment")
        cols = st.columns(4)
        scores = [
            c.number_input(name, 0, 100, default, key=f"s{i}")
            for i, (c, (name, default)) in enumerate(zip(cols, SECTIONS))
        ]

        if st.button("Submit assessment", type="primary"):
            start = time.time()
            result = submit_assessment(
                student_id, [{"name": n, "percent": int(v)} for (n, _), v in zip(SECTIONS, scores)]
            )
            elapsed_ms = int((time.time() - start) * 1000)
            st.session_state.submitted = True
            st.session_state.last_result = result
            st.session_state.last_ms = elapsed_ms

        if st.session_state.submitted:
            result = st.session_state.last_result
            ms = st.session_state.last_ms

            st.divider()
            st.subheader("2. Your results")

            overall = result["overallPercent"]
            m1, m2 = st.columns([1, 2])
            m1.metric("Overall score", f"{overall}%")
            m2.caption(
                f"Submitted in **{ms} ms**. The score is computed by Wasta's own system — "
                "the AI plan below is generated separately, in the background, and never "
                "affects it."
            )

            for s in result["sections"]:
                st.progress(s["percent"] / 100, text=f"{s['name']} — {s['percent']}%")

            st.divider()
            st.subheader("3. AI Career Coach")

            # Poll exactly like the real card does.
            placeholder = st.empty()
            plan = None
            for _ in range(20):
                plan = fetch_plan(student_id)
                if plan.get("status") != "pending":
                    break
                placeholder.info("Your study plan is being prepared…")
                time.sleep(2)

            placeholder.empty()

            if plan.get("status") == "ready":
                st.markdown(f"### {plan['headline']}")
                st.write(plan["assessment"])

                st.markdown("**Your 4-week plan**")
                for week in plan["weekly_plan"]:
                    with st.expander(f"Week {week['week']} — {week['focus']}", expanded=week["week"] == 1):
                        for action in week["actions"]:
                            st.markdown(f"- {action}")
                        st.caption(f"You'll know it's working when: {week['checkpoint']}")

                proj = plan["project_suggestion"]
                st.success(f"**Project to build — {proj['title']}**\n\n{proj['description']}")
                if proj.get("skills_practised"):
                    st.caption("Practises: " + " · ".join(proj["skills_practised"]))

                st.markdown("**What to say in an interview**")
                st.info(f"_{plan['interview_line']}_")

                st.caption(
                    "This plan is AI-generated study advice. Your Wasta Score is calculated "
                    "by our own system and is not affected by it."
                )
            elif plan.get("status") == "pending":
                st.info("Still generating — it usually lands within a minute.")
            else:
                st.caption(
                    "_(The coach card renders nothing when unavailable — the results page above "
                    "is intentionally complete without it.)_"
                )

# --------------------------------------------------------------------------
# Support chat
# --------------------------------------------------------------------------
with chat_tab:
    st.subheader("Support chat")
    st.caption(
        "Answers only from the curated knowledge base. It has no access to anyone's "
        "account data, and says so when asked."
    )

    for msg in st.session_state.messages:
        with st.chat_message(msg["role"]):
            st.markdown(msg["content"])

    if not st.session_state.messages:
        st.caption("Try: *what jobs are open?* · *how is my score calculated?* · *what is my score?*")

    if prompt := st.chat_input("Ask how something works…"):
        st.session_state.messages.append({"role": "user", "content": prompt})
        with st.chat_message("user"):
            st.markdown(prompt)

        with st.chat_message("assistant"):
            with st.spinner("Thinking…"):
                data = send_chat(student_id, prompt)
            reply = data.get("reply", "")
            st.markdown(reply)
            if data.get("outcome") not in ("answered", None):
                st.caption(f"outcome: `{data.get('outcome')}`")

        st.session_state.messages.append({"role": "assistant", "content": reply})
