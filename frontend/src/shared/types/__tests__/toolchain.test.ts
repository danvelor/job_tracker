import { expectTypeOf } from 'expect-type';

describe('toolchain', () => {
  it('runs Jest assertions', () => {
    expect(1 + 1).toBe(2);
  });

  it('runs expect-type assertions', () => {
    expectTypeOf<string>().toEqualTypeOf<string>();
    expectTypeOf<string>().not.toEqualTypeOf<number>();
  });

  it('has strictNullChecks on', () => {
    // @ts-expect-error null is not assignable to string under strict.
    const value: string = null;
    expect(value).toBeNull();
  });
});
