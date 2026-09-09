import { act, renderHook, waitFor } from '@testing-library/react';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import { createJobAction } from '../actions/create-job.action';
import { useCreateJob } from '../hooks/use-create-job.hook';

jest.mock('../actions/create-job.action', () => ({ createJobAction: jest.fn() }));

const action = jest.mocked(createJobAction);

const fill = (result: { current: ReturnType<typeof useCreateJob> }) => {
  act(() => result.current.change('title', 'Roof repair'));
  act(() => result.current.change('address.street', '12 Elm St'));
  act(() => result.current.change('address.city', 'Springfield'));
  act(() => result.current.change('address.state', 'IL'));
  act(() => result.current.change('address.zipCode', '62701'));
  act(() => result.current.change('address.latitude', '39.78'));
  act(() => result.current.change('address.longitude', '-89.65'));
  act(() => result.current.change('scheduledDate', '2099-03-14'));
  act(() => result.current.change('assigneeId', 'assignee-1'));
  act(() => result.current.change('customerId', 'customer-1'));
};

beforeEach(() => {
  action.mockReset();
  action.mockResolvedValue({ ok: true, id: 'job-new-1' });
});

describe('useCreateJob', () => {
  it('opens and closes the modal', () => {
    const { result } = renderHook(() => useCreateJob());
    expect(result.current.isOpen).toBe(false);

    act(() => result.current.open());
    expect(result.current.isOpen).toBe(true);

    act(() => result.current.close());
    expect(result.current.isOpen).toBe(false);
  });

  it('writes a nested field through its dot-notation path', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.change('address.city', 'Springfield'));

    expect(result.current.form.values.address.city).toBe('Springfield');
  });

  it('leaves the sibling address fields alone', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.change('address.city', 'Springfield'));
    act(() => result.current.change('address.street', '12 Elm St'));

    expect(result.current.form.values.address.city).toBe('Springfield');
    expect(result.current.form.values.address.street).toBe('12 Elm St');
  });

  it('records which fields have been blurred', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.blur('title'));

    expect(result.current.form.touched.title).toBe(true);
  });

  it('refuses to submit an invalid form and reveals every error', () => {
    const { result } = renderHook(() => useCreateJob());

    act(() => result.current.submit());

    expect(action).not.toHaveBeenCalled();
    expect(result.current.form.formError).toBe('Fix the highlighted fields');
    expect(Object.keys(result.current.form.errors).length).toBeGreaterThan(3);
  });

  it('converts the coordinates to numbers before calling the action', async () => {
    const { result } = renderHook(() => useCreateJob());
    fill(result);

    await act(async () => result.current.submit());

    expect(action).toHaveBeenCalledWith(
      expect.objectContaining({
        address: expect.objectContaining({ latitude: 39.78, longitude: -89.65 }),
      }),
    );
  });

  it('closes, resets and asks the view to revalidate on success', async () => {
    const invalidate = jest.fn();
    jobsEventBus.on('jobs:invalidate', invalidate);

    const { result } = renderHook(() => useCreateJob());
    act(() => result.current.open());
    fill(result);
    await act(async () => result.current.submit());

    await waitFor(() => expect(result.current.isOpen).toBe(false));
    expect(result.current.form.values.title).toBe('');
    expect(invalidate).toHaveBeenCalledTimes(1);
    jobsEventBus.off('jobs:invalidate', invalidate);
  });

  it('stays open and surfaces field errors on failure', async () => {
    action.mockResolvedValue({
      ok: false,
      error: {
        code: 'job.validation',
        message: 'A job cannot be scheduled in the past',
        kind: 'validation',
        fieldErrors: { scheduledDate: 'A job cannot be scheduled in the past' },
      },
    });

    const { result } = renderHook(() => useCreateJob());
    act(() => result.current.open());
    fill(result);
    await act(async () => result.current.submit());

    await waitFor(() =>
      expect(result.current.form.formError).toBe('A job cannot be scheduled in the past'),
    );
    expect(result.current.isOpen).toBe(true);
    expect(result.current.form.errors.scheduledDate).toBeDefined();
  });
});
