import { useEffect, useRef, useState } from "react";
import type { CoachPlanReady, CoachPlanResponse } from "./types";

const POLL_INTERVAL_MS = 5_000;
const GIVE_UP_AFTER_MS = 60_000;

export type CoachPlanViewState =
  | { kind: "loading" }
  | { kind: "pending" }
  | { kind: "ready"; plan: CoachPlanReady }
  | { kind: "unavailable" };

/**
 * Polls GET /api/students/me/coach-plan while the plan is still generating.
 * Stops polling once the plan is ready, the server reports it unavailable,
 * or 60 seconds pass with no result - at which point the caller should
 * render nothing, per spec, rather than an error state.
 */
export function useCoachPlan(): CoachPlanViewState {
  const [state, setState] = useState<CoachPlanViewState>({ kind: "loading" });
  const startedAtRef = useRef<number>(Date.now());

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function poll() {
      try {
        const response = await fetch("/api/students/me/coach-plan", {
          headers: { Accept: "application/json" },
        });

        if (!response.ok) {
          if (!cancelled) setState({ kind: "unavailable" });
          return;
        }

        const data = (await response.json()) as CoachPlanResponse;
        if (cancelled) return;

        if (data.status === "ready") {
          setState({ kind: "ready", plan: data });
          return;
        }

        if (data.status === "unavailable") {
          setState({ kind: "unavailable" });
          return;
        }

        // still pending
        setState({ kind: "pending" });
        const elapsed = Date.now() - startedAtRef.current;
        if (elapsed + POLL_INTERVAL_MS >= GIVE_UP_AFTER_MS) {
          timer = setTimeout(() => {
            if (!cancelled) setState({ kind: "unavailable" });
          }, GIVE_UP_AFTER_MS - elapsed);
          return;
        }

        timer = setTimeout(poll, POLL_INTERVAL_MS);
      } catch {
        if (!cancelled) setState({ kind: "unavailable" });
      }
    }

    poll();

    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, []);

  return state;
}
