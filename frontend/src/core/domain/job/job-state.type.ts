export type JobState =
  | { readonly status: 'Draft'; readonly notes?: string }
  | {
      readonly status: 'Scheduled';
      readonly scheduledDate: Date;
      readonly assigneeId: string;
    }
  | {
      readonly status: 'InProgress';
      readonly startedAt: Date;
      readonly assigneeId: string;
      readonly photos: readonly string[];
    }
  | {
      readonly status: 'Completed';
      readonly startedAt: Date;
      readonly completedAt: Date;
      readonly assigneeId: string;
      readonly photos: readonly string[];
      readonly signatureUrl: string;
    }
  | {
      readonly status: 'Cancelled';
      readonly cancelledAt: Date;
      readonly reason: string;
    };

export type JobAction =
  | {
      readonly type: 'SCHEDULE';
      readonly scheduledDate: Date;
      readonly assigneeId: string;
    }
  | { readonly type: 'START'; readonly startedAt: Date }
  | {
      readonly type: 'COMPLETE';
      readonly completedAt: Date;
      readonly signatureUrl: string;
    }
  | {
      readonly type: 'CANCEL';
      readonly cancelledAt: Date;
      readonly reason: string;
    };

export type AllowedAction = {
  Draft: 'SCHEDULE';
  Scheduled: 'START' | 'CANCEL';
  InProgress: 'COMPLETE' | 'CANCEL';
  Completed: never;
  Cancelled: never;
};

type TransitionTarget = {
  SCHEDULE: 'Scheduled';
  START: 'InProgress';
  COMPLETE: 'Completed';
  CANCEL: 'Cancelled';
};

export type ActionFor<S extends JobState> = Extract<
  JobAction,
  { type: AllowedAction[S['status']] }
>;

export type ResultOf<A extends JobAction> = Extract<
  JobState,
  { status: TransitionTarget[A['type']] }
>;
