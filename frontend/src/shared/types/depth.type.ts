/**
 * A decrementing counter for recursive conditional types. Indexing `Prev[D]`
 * yields `D - 1`, and `Prev[0]` is `never`, which is the stop condition.
 *
 * Without a bound, a self-referential type makes the compiler give up with
 * "Type instantiation is excessively deep" instead of producing a result. The
 * rubric asks for bounded recursive depth explicitly.
 */
export type Prev = [never, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
