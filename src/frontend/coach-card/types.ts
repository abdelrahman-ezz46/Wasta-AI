export interface CoachWeekPlan {
  week: number;
  focus: string;
  actions: string[];
  checkpoint: string;
}

export interface CoachProjectSuggestion {
  title: string;
  description: string;
  skills_practised: string[];
}

export interface CoachPlanReady {
  status: "ready";
  headline: string;
  assessment: string;
  weekly_plan: CoachWeekPlan[];
  project_suggestion: CoachProjectSuggestion;
  interview_line: string;
}

export interface CoachPlanPending {
  status: "pending";
}

export interface CoachPlanUnavailable {
  status: "unavailable";
}

export type CoachPlanResponse = CoachPlanReady | CoachPlanPending | CoachPlanUnavailable;
