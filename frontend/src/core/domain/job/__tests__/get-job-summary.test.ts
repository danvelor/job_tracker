import { getJobSummary } from '../get-job-summary';
import type { JobState } from '../job-state.type';

describe('getJobSummary', () => {
  it.each<[JobState, string]>([
    [{ status: 'Draft' }, 'Draft, not yet scheduled'],
    [
      {
        status: 'Scheduled',
        scheduledDate: new Date('2026-03-14T00:00:00Z'),
        assigneeId: 'a1',
      },
      'Scheduled for 2026-03-14, assigned to a1',
    ],
    [
      {
        status: 'InProgress',
        startedAt: new Date('2026-03-14T10:00:00Z'),
        assigneeId: 'a1',
        photos: ['p1', 'p2'],
      },
      'In progress since 2026-03-14, 2 photos',
    ],
    [
      {
        status: 'Completed',
        startedAt: new Date('2026-03-14T10:00:00Z'),
        completedAt: new Date('2026-03-14T16:00:00Z'),
        assigneeId: 'a1',
        photos: [],
        signatureUrl: 'sig.png',
      },
      'Completed on 2026-03-14, signed',
    ],
    [
      {
        status: 'Cancelled',
        cancelledAt: new Date('2026-03-14T11:00:00Z'),
        reason: 'Weather',
      },
      'Cancelled on 2026-03-14: Weather',
    ],
  ])('summarises the %# state', (state, expected) => {
    expect(getJobSummary(state)).toBe(expected);
  });

  it('includes the optional Draft note when present', () => {
    expect(getJobSummary({ status: 'Draft', notes: 'call first' })).toBe(
      'Draft, not yet scheduled: call first',
    );
  });
});
