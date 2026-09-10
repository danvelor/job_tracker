import type { JobState } from './job-state.type';

const asDay = (value: Date): string => value.toISOString().slice(0, 10);

export function getJobSummary(state: JobState): string {
  switch (state.status) {
    case 'Draft':
      return state.notes === undefined
        ? 'Draft, not yet scheduled'
        : `Draft, not yet scheduled: ${state.notes}`;

    case 'Scheduled':
      return `Scheduled for ${asDay(state.scheduledDate)}, assigned to ${state.assigneeId}`;

    case 'InProgress':
      return `In progress since ${asDay(state.startedAt)}, ${state.photos.length} photos`;

    case 'Completed':
      return `Completed on ${asDay(state.completedAt)}, signed`;

    case 'Cancelled':
      return `Cancelled on ${asDay(state.cancelledAt)}: ${state.reason}`;

    default: {
      const exhaustive: never = state;
      return exhaustive;
    }
  }
}
