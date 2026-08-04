export type ChatRole = "user" | "assistant";

export interface ChatMessage {
  id: string;
  role: ChatRole;
  content: string;
  pending?: boolean;
}

export type SendOutcome =
  | "answered"
  | "invalid_message"
  | "session_limit_reached"
  | "rate_limited"
  | "provider_unavailable";

export interface CreateSessionResponse {
  sessionId: string;
}

export interface SendMessageResponse {
  outcome: SendOutcome;
  reply: string;
}

export interface ChatMessageResponse {
  role: ChatRole;
  content: string;
  createdAt: string;
}
