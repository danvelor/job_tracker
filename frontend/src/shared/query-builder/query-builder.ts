export type ComparisonOperator =
  | 'eq'
  | 'neq'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'like'
  | 'in';

type Direction = 'asc' | 'desc';

/**
 * A chainable builder that narrows the type at every step.
 *
 * `select` narrows `Selected`, so `where` and `orderBy` accept only selected
 * fields. `value: T[K]` ties the third argument to the field's own type.
 * `Query` accumulates as a template literal type, and `build()` returns it —
 * which is assignable to the `string` assessment line 56 specifies, so both
 * requirements hold at once.
 *
 * `Op`, `Dir` and `N` are generic parameters rather than plain ones. Written
 * as `operator: ComparisonOperator`, the accumulated type would splice the
 * whole eight-member union into the literal and produce eight strings, then
 * sixteen, then thirty-two down the chain. Capturing the argument's literal
 * type is what makes the rendered type assertable.
 *
 * Its role in the application is stated in architecture 5.7: the compile-time
 * validation is what is used; the rendered SQL is asserted in tests and
 * mirrored by database/queries.sql, and is never sent from the browser.
 */
export class QueryBuilder<
  T,
  Selected extends keyof T = keyof T,
  Query extends string = '',
> {
  readonly #query: string;
  readonly #params: readonly unknown[];

  constructor(query = '', params: readonly unknown[] = []) {
    this.#query = query;
    this.#params = params;
  }

  select<K extends keyof T & string>(
    ...fields: readonly [K, ...K[]]
  ): QueryBuilder<T, K, `SELECT ${string}`> {
    return new QueryBuilder<T, K, `SELECT ${string}`>(
      `SELECT ${fields.join(', ')}`,
      this.#params,
    );
  }

  where<K extends Selected & string, Op extends ComparisonOperator>(
    field: K,
    operator: Op,
    value: T[K],
  ): QueryBuilder<T, Selected, `${Query} WHERE ${K} ${Op}`> {
    return new QueryBuilder<T, Selected, `${Query} WHERE ${K} ${Op}`>(
      `${this.#query} WHERE ${field} ${operator}`,
      [...this.#params, value],
    );
  }

  orderBy<K extends Selected & string, Dir extends Direction>(
    field: K,
    direction: Dir,
  ): QueryBuilder<T, Selected, `${Query} ORDER BY ${K} ${Uppercase<Dir>}`> {
    return new QueryBuilder<T, Selected, `${Query} ORDER BY ${K} ${Uppercase<Dir>}`>(
      `${this.#query} ORDER BY ${field} ${direction.toUpperCase()}`,
      this.#params,
    );
  }

  limit<N extends number>(
    count: N,
  ): QueryBuilder<T, Selected, `${Query} LIMIT ${N}`> {
    return new QueryBuilder<T, Selected, `${Query} LIMIT ${N}`>(
      `${this.#query} LIMIT ${count}`,
      this.#params,
    );
  }

  build(): { query: Query; params: readonly unknown[] } {
    // The single assertion in this file. The runtime string is built by the
    // same steps that build `Query`, so the two agree by construction, but the
    // compiler cannot verify a string it did not compute. This is a narrowing
    // assertion, not the `as unknown as X` that CLAUDE.md forbids, and the
    // type test is what keeps the two in step.
    return { query: this.#query as Query, params: this.#params };
  }
}
