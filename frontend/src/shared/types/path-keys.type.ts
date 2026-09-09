import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

/**
 * What counts as the end of a path. An array, a Map, a Set or a Date is
 * addressed as a whole rather than descended into: `PathKeys` exists to name
 * form fields, and a field is a value a user types, not a container.
 */
type Leaf =
  | Primitive
  | Date
  | ReadonlyArray<unknown>
  | Map<unknown, unknown>
  | Set<unknown>
  | ((...args: never[]) => unknown);

/**
 * A union of every dot-notation path to a leaf property.
 *
 *   PathKeys<{ a: { b: string; c: { d: number } } }>  =>  "a.b" | "a.c.d"
 *
 * Leaf paths only: the intermediate `"a"` is not emitted, per the example in
 * assessment line 33.
 *
 * `D` bounds the recursion for the same reason `DeepReadonly` does, reusing
 * the same `Prev`. Two recursive utilities side by side with only one of them
 * bounded reads as an oversight.
 */
export type PathKeys<T, D extends Prev[number] = 9> = [D] extends [never]
  ? never
  : {
      [K in keyof T & string]: T[K] extends Leaf
        ? K
        : `${K}.${PathKeys<T[K], Prev[D]>}`;
    }[keyof T & string];
