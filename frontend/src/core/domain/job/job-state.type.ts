/**
 * The five states a job can be in, each carrying exactly the data that state
 * holds. Discriminated on `status`.
 */
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

/**
 * The transition table lives in the type system rather than in a switch.
 * `never` for the terminal states is what makes them terminal: there is no
 * action a caller can supply.
 */
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

/** The actions a given state permits. `never` for a terminal state. */
export type ActionFor<S extends JobState> = Extract<
  JobAction,
  { type: AllowedAction[S['status']] }
>;

/** The state a given action produces. */
export type ResultOf<A extends JobAction> = Extract<
  JobState,
  { status: TransitionTarget[A['type']] }
>;
