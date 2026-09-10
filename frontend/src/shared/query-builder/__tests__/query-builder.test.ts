import { expectTypeOf } from 'expect-type';
import { QueryBuilder } from '../query-builder';

type Job = {
  id: string;
  title: string;
  status: 'Scheduled' | 'Completed';
  photoCount: number;
};

describe('QueryBuilder', () => {
  it('renders the chained query', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'title', 'status')
      .where('status', 'eq', 'Completed')
      .orderBy('title', 'asc')
      .limit(10)
      .build();

    expect(result.query).toBe(
      'SELECT id, title, status WHERE status eq ORDER BY title ASC LIMIT 10',
    );
  });

  it('collects the bound values as params in order', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'status', 'photoCount')
      .where('status', 'eq', 'Scheduled')
      .where('photoCount', 'gt', 3)
      .build();

    expect(result.params).toEqual(['Scheduled', 3]);
  });

  it('accumulates the query as a template literal type', () => {
    const result = new QueryBuilder<Job>()
      .select('id', 'title')
      .where('title', 'like', 'roof')
      .orderBy('title', 'desc')
      .limit(5)
      .build();

    expectTypeOf(result.query).toEqualTypeOf<
      `SELECT ${string} WHERE title like ORDER BY title DESC LIMIT 5`
    >();
  });

  it('rejects a where on a field that was not selected', () => {
    const builder = new QueryBuilder<Job>().select('id', 'title');
    // @ts-expect-error photoCount was not selected
    builder.where('photoCount', 'gt', 1);
    expect(builder).toBeDefined();
  });

  it('rejects an orderBy on a field that was not selected', () => {
    const builder = new QueryBuilder<Job>().select('id', 'title');
    // @ts-expect-error status was not selected
    builder.orderBy('status', 'asc');
    expect(builder).toBeDefined();
  });

  it('rejects a value that does not match the field type', () => {
    const builder = new QueryBuilder<Job>().select('id', 'photoCount');
    // @ts-expect-error photoCount is a number
    builder.where('photoCount', 'eq', 'three');
    expect(builder).toBeDefined();
  });

  it('rejects a value outside a literal union field type', () => {
    const builder = new QueryBuilder<Job>().select('id', 'status');
    // @ts-expect-error 'Draft' is not a member of the status union
    builder.where('status', 'eq', 'Draft');
    expect(builder).toBeDefined();
  });
});
