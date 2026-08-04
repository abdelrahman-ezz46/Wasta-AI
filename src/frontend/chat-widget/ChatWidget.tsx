import { useState } from "react";
import styles from "./ChatWidget.module.css";
import { useChatSession } from "./useChatSession";

/**
 * Floating support chat launcher, available on every page - no login
 * required. Session id lives in localStorage so a page reload keeps the
 * same conversation; the visitor id lets a returning anonymous visitor's
 * sessions be told apart without an account.
 */
export function ChatWidget() {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState("");
  const { messages, sendMessage, sending, ready, notice } = useChatSession();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!draft.trim() || sending) return;
    void sendMessage(draft);
    setDraft("");
  }

  if (!open) {
    return (
      <button
        type="button"
        className={styles.launcher}
        onClick={() => setOpen(true)}
        aria-label="Open support chat"
      >
        ?
      </button>
    );
  }

  return (
    <div className={styles.panel} role="dialog" aria-label="Support chat">
      <div className={styles.header}>
        <p className={styles.headerTitle}>Wasta support</p>
        <button type="button" className={styles.closeButton} onClick={() => setOpen(false)} aria-label="Close chat">
          ✕
        </button>
      </div>

      <div className={styles.messages} aria-live="polite">
        {messages.map((m) => (
          <div key={m.id} className={`${styles.bubbleRow} ${m.role === "user" ? styles.bubbleRowUser : ""}`}>
            <div className={`${styles.bubble} ${m.role === "user" ? styles.bubbleUser : styles.bubbleAssistant}`}>
              {m.content}
            </div>
          </div>
        ))}
        {notice && <p className={styles.notice}>{notice}</p>}
      </div>

      <form className={styles.inputRow} onSubmit={handleSubmit}>
        <input
          className={styles.input}
          type="text"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Ask how something works..."
          disabled={!ready}
          aria-label="Message"
        />
        <button type="submit" className={styles.sendButton} disabled={!ready || sending || !draft.trim()}>
          Send
        </button>
      </form>
    </div>
  );
}
