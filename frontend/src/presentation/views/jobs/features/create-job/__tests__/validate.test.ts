import { EMPTY_VALUES, validate } from '../hooks/use-create-job.hook';

const valid = {
  title: 'Roof repair',
  description: '',
  address: {
    street: '12 Elm St',
    city: 'Springfield',
    state: 'IL',
    zipCode: '62701',
    latitude: '39.78',
    longitude: '-89.65',
  },
  scheduledDate: '2099-03-14',
  assigneeId: 'assignee-1',
  customerId: 'customer-1',
};

describe('validate', () => {
  it('accepts a complete form', () => {
    expect(validate(valid)).toEqual({});
  });

  it('requires a title', () => {
    expect(validate({ ...valid, title: '  ' }).title).toBe('A title is required');
  });

  it('requires every address field', () => {
    const errors = validate({ ...valid, address: { ...valid.address, city: '' } });

    expect(errors['address.city']).toBe('City is required');
  });

  it('requires coordinates to be numbers', () => {
    const errors = validate({ ...valid, address: { ...valid.address, latitude: 'north' } });

    expect(errors['address.latitude']).toBe('Latitude must be a number');
  });

  it('requires a scheduled date', () => {
    expect(validate({ ...valid, scheduledDate: '' }).scheduledDate).toBe(
      'A scheduled date is required',
    );
  });

  it('refuses a date in the past (BR-1)', () => {
    expect(validate({ ...valid, scheduledDate: '2000-01-01' }).scheduledDate).toBe(
      'A job cannot be scheduled in the past',
    );
  });

  it('requires an assignee and a customer', () => {
    const errors = validate({ ...valid, assigneeId: '', customerId: '' });

    expect(errors.assigneeId).toBe('An assignee is required');
    expect(errors.customerId).toBe('A customer is required');
  });

  it('reports every problem at once rather than the first', () => {
    expect(Object.keys(validate(EMPTY_VALUES)).length).toBeGreaterThan(3);
  });

  it('leaves description optional', () => {
    expect(validate({ ...valid, description: '' })).toEqual({});
  });
});
