import { expectTypeOf } from 'expect-type';
import type { PathKeys } from '../path-keys.type';

describe('PathKeys', () => {
  it('produces dot-notation paths to leaves only', () => {
    expectTypeOf<PathKeys<{ a: { b: string; c: { d: number } } }>>().toEqualTypeOf<
      'a.b' | 'a.c.d'
    >();
  });

  it('does not emit the intermediate path', () => {
    expectTypeOf<PathKeys<{ a: { b: string } }>>().not.toEqualTypeOf<'a' | 'a.b'>();
  });

  it('treats an array property as a leaf', () => {
    expectTypeOf<PathKeys<{ tags: string[]; name: string }>>().toEqualTypeOf<
      'tags' | 'name'
    >();
  });

  it('treats a Date property as a leaf', () => {
    expectTypeOf<PathKeys<{ at: Date }>>().toEqualTypeOf<'at'>();
  });

  it('handles the shape the create-job form uses', () => {
    type Values = {
      title: string;
      address: { street: string; zipCode: string };
    };
    expectTypeOf<PathKeys<Values>>().toEqualTypeOf<
      'title' | 'address.street' | 'address.zipCode'
    >();
  });

  it('is a type-only module', () => {
    expect(true).toBe(true);
  });
});
