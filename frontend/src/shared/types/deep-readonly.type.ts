import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

/**
 * Recursively marks every property readonly, including nested objects, arrays,
 * Maps, Sets and tuples.
 *
 * Two orderings matter. The tuple case precedes the array case because a tuple
 * also matches `ReadonlyArray` and would otherwise lose its positional types.
 * `Date` and functions are treated as leaves, because freezing their members
 * means nothing.
 *
 * `D` bounds the recursion so a self-referential type resolves rather than
 * exhausting the compiler.
 */
export type DeepReadonly<T, D extends Prev[number] = 9> = [D] extends [never]
  ? T
  : T extends Primitive
    ? T
    : T extends (...args: never[]) => unknown
      ? T
      : T extends Date
        ? T
        : T extends Map<infer K, infer V>
          ? ReadonlyMap<K, DeepReadonly<V, Prev[D]>>
          : T extends Set<infer V>
            ? ReadonlySet<DeepReadonly<V, Prev[D]>>
            : T extends readonly [unknown, ...unknown[]]
              ? { readonly [I in keyof T]: DeepReadonly<T[I], Prev[D]> }
              : T extends ReadonlyArray<infer I>
                ? ReadonlyArray<DeepReadonly<I, Prev[D]>>
                : { readonly [K in keyof T]: DeepReadonly<T[K], Prev[D]> };
