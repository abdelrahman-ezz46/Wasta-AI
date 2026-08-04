import { useCallback, useEffect, useRef, useState } from "react";
import type { ChatMessage, ChatMessageResponse, CreateSessionResponse, SendMessageResponse } from "./types";

const VISITOR_ID_KEY = "wasta.chat.visitorId";
const SESSION_ID_KEY = "wasta.chat.sessionId";
const VISITOR_ID_HEADER = "X-Wasta-Visitor-Id";

function getOrCreateVisitorId(): string {
  let id = localStorage.getItem(VISITOR_ID_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(VISITOR_ID_KEY, id);
  }
  return id;
}

/**
 * Anonymous sessions prove ownership by echoing back the visitor id they
 * were created with - the session id alone isn't accepted, since it travels
 * in the URL path and ends up in server and proxy logs. Logged-in students
 * are authorized server-side from their auth instead, so this header is
 * simply ignored for them.
 */
function sessionHeaders(extra?: Record<string, string>): Record<string, string> {
  return { [VISITOR_ID_HEADER]: getOrCreateVisitorId(), ...extra };
}

let nextLocalId = 0;

export function useChatSession() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [sending, setSending] = useState(false);
  const [ready, setReady] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const sessionIdRef = useRef<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function init() {
      const existingSessionId = localStorage.getItem(SESSION_ID_KEY);

      if (existingSessionId) {
        sessionIdRef.current = existingSessionId;
        try {
          const response = await fetch(`/api/chat/sessions/${existingSessionId}/messages`, {
            headers: sessionHeaders(),
          });
          if (response.ok) {
            const history = (await response.json()) as ChatMessageResponse[];
            if (!cancelled) {
              setMessages(history.map((m) => ({ id: `${nextLocalId++}`, role: m.role, content: m.content })));
              setReady(true);
            }
            return;
          }
        } catch {
          // fall through to creating a fresh session below
        }
      }

      try {
        const response = await fetch("/api/chat/sessions", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ visitorId: getOrCreateVisitorId() }),
        });
        const data = (await response.json()) as CreateSessionResponse;
        localStorage.setItem(SESSION_ID_KEY, data.sessionId);
        sessionIdRef.current = data.sessionId;
        if (!cancelled) setReady(true);
      } catch {
        if (!cancelled) setNotice("Chat is unavailable right now.");
      }
    }

    init();
    return () => {
      cancelled = true;
    };
  }, []);

  const sendMessage = useCallback(async (text: string) => {
    const sessionId = sessionIdRef.current;
    if (!sessionId || !text.trim()) return;

    const userMessage: ChatMessage = { id: `${nextLocalId++}`, role: "user", content: text.trim() };
    setMessages((prev) => [...prev, userMessage]);
    setSending(true);
    setNotice(null);

    try {
      const response = await fetch(`/api/chat/sessions/${sessionId}/messages`, {
        method: "POST",
        headers: sessionHeaders({ "Content-Type": "application/json" }),
        body: JSON.stringify({ message: userMessage.content }),
      });

      if (!response.ok) {
        setNotice("Couldn't send that message. Please try again.");
        return;
      }

      const data = (await response.json()) as SendMessageResponse;

      if (data.outcome === "answered") {
        setMessages((prev) => [...prev, { id: `${nextLocalId++}`, role: "assistant", content: data.reply }]);
      } else {
        setNotice(data.reply);
      }
    } catch {
      setNotice("Couldn't send that message. Please try again.");
    } finally {
      setSending(false);
    }
  }, []);

  return { messages, sendMessage, sending, ready, notice };
}
