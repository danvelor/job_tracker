import { expectTypeOf } from 'expect-type';
import { createTypedEventEmitter } from '../typed-event-emitter';

type TestEvents = {
  'job:status-changed': { jobId: string; status: string };
  'jobs:invalidate': void;
};

describe('createTypedEventEmitter', () => {
  it('delivers a payload to a subscribed handler', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const received: { jobId: string; status: string }[] = [];

    emitter.on('job:status-changed', (payload) => received.push(payload));
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(received).toEqual([{ jobId: 'j1', status: 'Completed' }]);
  });

  it('delivers to every subscriber of the same event', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];

    emitter.on('job:status-changed', () => calls.push('first'));
    emitter.on('job:status-changed', () => calls.push('second'));
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual(['first', 'second']);
  });

  it('stops delivering after off with the same handler reference', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];
    const handler = () => calls.push('called');

    emitter.on('job:status-changed', handler);
    emitter.off('job:status-changed', handler);
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual([]);
  });

  it('leaves other handlers subscribed when one is removed', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    const calls: string[] = [];
    const removed = () => calls.push('removed');

    emitter.on('job:status-changed', removed);
    emitter.on('job:status-changed', () => calls.push('kept'));
    emitter.off('job:status-changed', removed);
    emitter.emit('job:status-changed', { jobId: 'j1', status: 'Completed' });

    expect(calls).toEqual(['kept']);
  });

  it('does nothing when emitting an event with no subscribers', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    expect(() => emitter.emit('jobs:invalidate', undefined)).not.toThrow();
  });

  it('ignores off for a handler that was never subscribed', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    expect(() => emitter.off('jobs:invalidate', () => undefined)).not.toThrow();
  });

  it('types the handler payload from the event name', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    emitter.on('job:status-changed', (payload) => {
      expectTypeOf(payload).toEqualTypeOf<{ jobId: string; status: string }>();
    });
    expect(emitter).toBeDefined();
  });

  it('rejects a payload that does not match the event', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    // @ts-expect-error the payload for job:status-changed is not a string
    emitter.emit('job:status-changed', 'wrong');
    expect(emitter).toBeDefined();
  });

  it('rejects an unknown event name', () => {
    const emitter = createTypedEventEmitter<TestEvents>();
    // @ts-expect-error 'job:exploded' is not in the event map
    emitter.on('job:exploded', () => undefined);
    expect(emitter).toBeDefined();
  });
});
