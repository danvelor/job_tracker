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
    return { query: this.#query as Query, params: this.#params };
  }
}
