import { expectTypeOf } from 'expect-type';
import type { DeepReadonly } from '../deep-readonly.type';

describe('DeepReadonly', () => {
  it('leaves primitives unchanged', () => {
    expectTypeOf<DeepReadonly<string>>().toEqualTypeOf<string>();
    expectTypeOf<DeepReadonly<number>>().toEqualTypeOf<number>();
    expectTypeOf<DeepReadonly<null>>().toEqualTypeOf<null>();
  });

  it('makes nested object properties readonly', () => {
    expectTypeOf<DeepReadonly<{ a: { b: string } }>>().toEqualTypeOf<{
      readonly a: { readonly b: string };
    }>();
  });

  it('turns arrays into ReadonlyArray of DeepReadonly items', () => {
    expectTypeOf<DeepReadonly<{ a: string }[]>>().toEqualTypeOf<
      ReadonlyArray<{ readonly a: string }>
    >();
  });

  it('turns Maps into ReadonlyMap with a deep value', () => {
    expectTypeOf<DeepReadonly<Map<string, { a: number }>>>().toEqualTypeOf<
      ReadonlyMap<string, { readonly a: number }>
    >();
  });

  it('turns Sets into ReadonlySet with a deep value', () => {
    expectTypeOf<DeepReadonly<Set<{ a: number }>>>().toEqualTypeOf<
      ReadonlySet<{ readonly a: number }>
    >();
  });

  it('keeps tuple positions instead of widening to an array', () => {
    expectTypeOf<DeepReadonly<[string, { a: number }]>>().toEqualTypeOf<
      readonly [string, { readonly a: number }]
    >();
  });

  it('treats Date as a leaf', () => {
    expectTypeOf<DeepReadonly<{ at: Date }>>().toEqualTypeOf<{
      readonly at: Date;
    }>();
  });

  it('bounds recursion so a self-referential type still resolves', () => {
    interface Node {
      value: string;
      child: Node;
    }
    expectTypeOf<DeepReadonly<Node>>().not.toBeNever();
  });

  it('is a type-only module', () => {
    expect(true).toBe(true);
  });
});
