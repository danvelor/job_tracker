import { err, isOk, ok } from '../result';
import type { CoreError } from '../result.type';

const failure: CoreError = {
  code: 'job.not-found',
  message: 'No such job',
  kind: 'not-found',
};

describe('Result', () => {
  it('carries a value on success', () => {
    const result = ok(42);

    expect(result.ok).toBe(true);
    expect(isOk(result)).toBe(true);
    if (isOk(result)) {
      expect(result.value).toBe(42);
    }
  });

  it('carries an error on failure', () => {
    const result = err(failure);

    expect(result.ok).toBe(false);
    expect(isOk(result)).toBe(false);
    if (!isOk(result)) {
      expect(result.error.kind).toBe('not-found');
    }
  });

  it('narrows the union through the guard', () => {
    const result: ReturnType<typeof ok<string>> | ReturnType<typeof err<CoreError>> =
      Math.random() > 2 ? ok('yes') : err(failure);

    const rendered = isOk(result) ? result.value : result.error.message;

    expect(typeof rendered).toBe('string');
  });
});
