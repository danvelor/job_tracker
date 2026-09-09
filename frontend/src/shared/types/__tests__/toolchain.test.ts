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
    // This fails the build if anyone turns strict off, which is what makes
    // the assertion worth having rather than decorative.
    const value: string = null;
    expect(value).toBeNull();
  });
});
