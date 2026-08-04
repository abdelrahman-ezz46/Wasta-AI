using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Services;

public sealed record CoachResult(bool Success, StudentCoachPlan? Plan, string? Error);
