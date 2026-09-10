import { expectTypeOf } from 'expect-type';
import { allowedActionsFor } from '../allowed-actions';
import type { AllowedAction } from '../job-state.type';

describe('allowedActionsFor', () => {
  it('offers SCHEDULE from Draft', () => {
    expect(allowedActionsFor('Draft')).toEqual(['SCHEDULE']);
  });

  it('offers START and CANCEL from Scheduled', () => {
    expect(allowedActionsFor('Scheduled')).toEqual(['START', 'CANCEL']);
  });

  it('offers COMPLETE and CANCEL from InProgress', () => {
    expect(allowedActionsFor('InProgress')).toEqual(['COMPLETE', 'CANCEL']);
  });

  it('offers nothing from a terminal state (BR-2)', () => {
    expect(allowedActionsFor('Completed')).toEqual([]);
    expect(allowedActionsFor('Cancelled')).toEqual([]);
  });

  it('agrees with the type-level transition table', () => {
    expectTypeOf<AllowedAction['Scheduled']>().toEqualTypeOf<'START' | 'CANCEL'>();
    expectTypeOf<AllowedAction['InProgress']>().toEqualTypeOf<'COMPLETE' | 'CANCEL'>();
    expectTypeOf<AllowedAction['Completed']>().toBeNever();
    expectTypeOf<AllowedAction['Cancelled']>().toBeNever();
  });
});
