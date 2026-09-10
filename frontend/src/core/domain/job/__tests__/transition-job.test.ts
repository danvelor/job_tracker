import { transitionJob } from '../transition-job';
import type { JobAction, JobState } from '../job-state.type';

const scheduledAt = new Date('2026-03-14T09:00:00Z');
const startedAt = new Date('2026-03-14T10:00:00Z');
const completedAt = new Date('2026-03-14T16:00:00Z');
const cancelledAt = new Date('2026-03-14T11:00:00Z');

const draft = { status: 'Draft' } as const satisfies JobState;
const scheduled = {
  status: 'Scheduled',
  scheduledDate: scheduledAt,
  assigneeId: 'a1',
} as const satisfies JobState;
const inProgress = {
  status: 'InProgress',
  startedAt,
  assigneeId: 'a1',
  photos: [],
} as const satisfies JobState;

describe('transitionJob', () => {
  it('moves Draft to Scheduled', () => {
    const next = transitionJob(draft, {
      type: 'SCHEDULE',
      scheduledDate: scheduledAt,
      assigneeId: 'a1',
    });

    expect(next).toEqual({
      status: 'Scheduled',
      scheduledDate: scheduledAt,
      assigneeId: 'a1',
    });
  });

  it('moves Scheduled to InProgress and keeps the assignee', () => {
    const next = transitionJob(scheduled, { type: 'START', startedAt });

    expect(next).toEqual({
      status: 'InProgress',
      startedAt,
      assigneeId: 'a1',
      photos: [],
    });
  });

  it('moves Scheduled to Cancelled with the reason', () => {
    const next = transitionJob(scheduled, {
      type: 'CANCEL',
      cancelledAt,
      reason: 'Weather',
    });

    expect(next).toEqual({ status: 'Cancelled', cancelledAt, reason: 'Weather' });
  });

  it('moves InProgress to Completed carrying photos and signature', () => {
    const withPhoto = { ...inProgress, photos: ['p1.jpg'] } as const;
    const next = transitionJob(withPhoto, {
      type: 'COMPLETE',
      completedAt,
      signatureUrl: 'sig.png',
    });

    expect(next).toEqual({
      status: 'Completed',
      startedAt,
      completedAt,
      assigneeId: 'a1',
      photos: ['p1.jpg'],
      signatureUrl: 'sig.png',
    });
  });

  it('moves InProgress to Cancelled', () => {
    const next = transitionJob(inProgress, {
      type: 'CANCEL',
      cancelledAt,
      reason: 'Customer withdrew',
    });

    expect(next.status).toBe('Cancelled');
  });

  it('refuses at compile time to start a Draft', () => {
    const invalid = () => {
      // @ts-expect-error Draft only allows SCHEDULE
      transitionJob(draft, { type: 'START', startedAt });
    };

    expect(invalid).toBeInstanceOf(Function);
  });

  it('refuses at compile time to complete a Scheduled job', () => {
    const invalid = () => {
      // @ts-expect-error only an InProgress job can complete
      transitionJob(scheduled, { type: 'COMPLETE', completedAt, signatureUrl: 's' });
    };

    expect(invalid).toBeInstanceOf(Function);
  });

  it('refuses at compile time to transition out of Completed', () => {
    const completed = {
      status: 'Completed',
      startedAt,
      completedAt,
      assigneeId: 'a1',
      photos: [],
      signatureUrl: 'sig.png',
    } as const satisfies JobState;

    const invalid = () => {
      // @ts-expect-error Completed is terminal: ActionFor<Completed> is never
      transitionJob(completed, { type: 'CANCEL', cancelledAt, reason: 'no' });
    };

    expect(invalid).toBeInstanceOf(Function);
  });

  it('refuses at compile time to transition out of Cancelled', () => {
    const cancelled = {
      status: 'Cancelled',
      cancelledAt,
      reason: 'Weather',
    } as const satisfies JobState;

    const invalid = () => {
      // @ts-expect-error Cancelled is terminal
      transitionJob(cancelled, {
        type: 'SCHEDULE',
        scheduledDate: scheduledAt,
        assigneeId: 'a1',
      });
    };

    expect(invalid).toBeInstanceOf(Function);
  });

  it('guards against a state and action pair the types would have refused', () => {
    const unnarrowed: (state: JobState, action: JobAction) => JobState =
      transitionJob;

    expect(() => unnarrowed(draft, { type: 'START', startedAt })).toThrow(
      'expected Scheduled, received Draft',
    );
  });
});
