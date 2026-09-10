import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

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
