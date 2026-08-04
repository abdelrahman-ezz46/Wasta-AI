import styles from "./CoachCard.module.css";
import { useCoachPlan } from "./useCoachPlan";

/**
 * Renders below the existing score breakdown and static SectionFeedback
 * blurbs on the results page. Three states, per spec:
 *  - pending: a small "being prepared" indicator, polls in the background
 *  - ready: the full plan
 *  - unavailable (including the 60s give-up): renders nothing at all -
 *    the results page must look complete without this card.
 */
export function CoachCard() {
  const state = useCoachPlan();

  if (state.kind === "loading" || state.kind === "unavailable") {
    return null;
  }

  if (state.kind === "pending") {
    return (
      <div className={styles.pendingCard} role="status" aria-live="polite">
        <span className={styles.spinner} aria-hidden="true" />
        <span>Your study plan is being prepared</span>
      </div>
    );
  }

  const { plan } = state;

  return (
    <section className={styles.card} aria-label="AI career coach study plan">
      <div>
        <h3 className={styles.headline}>{plan.headline}</h3>
        <p className={styles.assessment}>{plan.assessment}</p>
      </div>

      <div>
        <p className={styles.sectionLabel}>Your 4-week plan</p>
        <ol className={styles.stepper}>
          {plan.weekly_plan.map((week) => (
            <li key={week.week} className={styles.step}>
              <span className={styles.stepNumber}>W{week.week}</span>
              <div className={styles.stepBody}>
                <p className={styles.stepFocus}>{week.focus}</p>
                <ul className={styles.stepActions}>
                  {week.actions.map((action, i) => (
                    <li key={i}>{action}</li>
                  ))}
                </ul>
                <p className={styles.stepCheckpoint}>
                  You&apos;ll know it&apos;s working when: {week.checkpoint}
                </p>
              </div>
            </li>
          ))}
        </ol>
      </div>

      <div>
        <p className={styles.sectionLabel}>Project to build</p>
        <div className={styles.projectCard}>
          <p className={styles.projectTitle}>{plan.project_suggestion.title}</p>
          <p className={styles.projectDescription}>{plan.project_suggestion.description}</p>
          <div className={styles.skillTags}>
            {plan.project_suggestion.skills_practised.map((skill) => (
              <span key={skill} className={styles.skillTag}>
                {skill}
              </span>
            ))}
          </div>
        </div>
      </div>

      <blockquote className={styles.pullQuote}>&ldquo;{plan.interview_line}&rdquo;</blockquote>

      <p className={styles.disclaimer}>
        This plan is AI-generated study advice. Your Wasta Score is calculated by our own system
        and is not affected by it.
      </p>
    </section>
  );
}
