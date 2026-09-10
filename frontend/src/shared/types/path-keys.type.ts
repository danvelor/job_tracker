import type { Prev } from './depth.type';
import type { Primitive } from './primitive.type';

type Leaf =
  | Primitive
  | Date
  | ReadonlyArray<unknown>
  | Map<unknown, unknown>
  | Set<unknown>
  | ((...args: never[]) => unknown);

export type PathKeys<T, D extends Prev[number] = 9> = [D] extends [never]
  ? never
  : {
      [K in keyof T & string]: T[K] extends Leaf
        ? K
        : `${K}.${PathKeys<T[K], Prev[D]>}`;
    }[keyof T & string];
