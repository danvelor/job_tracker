'use client';

import { useCallback, useReducer, useState, useTransition } from 'react';
import type { CreateJobInput } from '@/core/application/ports/jobs.port';
import { jobsEventBus } from '@/shared/events/jobs-event-bus';
import type { PathKeys } from '@/shared/types/path-keys.type';
import { createJobAction } from '../actions/create-job.action';

export type CreateJobValues = {
  title: string;
  description: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: string;
    longitude: string;
  };
  scheduledDate: string;
  assigneeId: string;
  customerId: string;
};

export type CreateJobField = PathKeys<CreateJobValues>;
export type CreateJobErrors = Partial<Record<CreateJobField, string>>;

export const EMPTY_VALUES: CreateJobValues = {
  title: '',
  description: '',
  address: { street: '', city: '', state: '', zipCode: '', latitude: '', longitude: '' },
  scheduledDate: '',
  assigneeId: '',
  customerId: '',
};

const isBlank = (value: string): boolean => value.trim() === '';

const isNumeric = (value: string): boolean => value !== '' && !Number.isNaN(Number(value));

export function validate(values: CreateJobValues): CreateJobErrors {
  const errors: Record<string, string> = {};

  if (isBlank(values.title)) errors.title = 'A title is required';
  if (isBlank(values.address.street)) errors['address.street'] = 'Street is required';
  if (isBlank(values.address.city)) errors['address.city'] = 'City is required';
  if (isBlank(values.address.state)) errors['address.state'] = 'State is required';
  if (isBlank(values.address.zipCode)) errors['address.zipCode'] = 'ZIP code is required';
  if (!isNumeric(values.address.latitude)) {
    errors['address.latitude'] = 'Latitude must be a number';
  }
  if (!isNumeric(values.address.longitude)) {
    errors['address.longitude'] = 'Longitude must be a number';
  }

  if (isBlank(values.scheduledDate)) {
    errors.scheduledDate = 'A scheduled date is required';
  } else if (values.scheduledDate < new Date().toISOString().slice(0, 10)) {
    errors.scheduledDate = 'A job cannot be scheduled in the past';
  }

  if (isBlank(values.assigneeId)) errors.assigneeId = 'An assignee is required';
  if (isBlank(values.customerId)) errors.customerId = 'A customer is required';

  return errors;
}

export type CreateJobFormState = {
  values: CreateJobValues;
  errors: CreateJobErrors;
  touched: Partial<Record<CreateJobField, boolean>>;
  status: 'idle' | 'submitting' | 'failed';
  formError: string | null;
};

export type CreateJobFormAction =
  | { type: 'FIELD_CHANGED'; field: CreateJobField; value: string }
  | { type: 'FIELD_BLURRED'; field: CreateJobField }
  | { type: 'SUBMIT_STARTED' }
  | { type: 'SUBMIT_FAILED'; formError: string; fieldErrors?: CreateJobErrors }
  | { type: 'SUBMIT_SUCCEEDED' }
  | { type: 'RESET' };

const INITIAL: CreateJobFormState = {
  values: EMPTY_VALUES,
  errors: {},
  touched: {},
  status: 'idle',
  formError: null,
};

const ADDRESS_PREFIX = 'address.';

const setField = (
  values: CreateJobValues,
  field: CreateJobField,
  value: string,
): CreateJobValues =>
  field.startsWith(ADDRESS_PREFIX)
    ? {
        ...values,
        address: { ...values.address, [field.slice(ADDRESS_PREFIX.length)]: value },
      }
    : { ...values, [field]: value };

export function createJobReducer(
  state: CreateJobFormState,
  action: CreateJobFormAction,
): CreateJobFormState {
  switch (action.type) {
    case 'FIELD_CHANGED': {
      const values = setField(state.values, action.field, action.value);
      return { ...state, values, errors: validate(values), formError: null };
    }
    case 'FIELD_BLURRED':
      return { ...state, touched: { ...state.touched, [action.field]: true } };
    case 'SUBMIT_STARTED':
      return { ...state, status: 'submitting', formError: null };
    case 'SUBMIT_FAILED':
      return {
        ...state,
        status: 'failed',
        formError: action.formError,
        errors: { ...state.errors, ...action.fieldErrors },
      };
    case 'SUBMIT_SUCCEEDED':
    case 'RESET':
      return INITIAL;
  }
}

const toInput = (values: CreateJobValues): CreateJobInput => ({
  title: values.title,
  description: values.description,
  address: {
    street: values.address.street,
    city: values.address.city,
    state: values.address.state,
    zipCode: values.address.zipCode,
    latitude: Number(values.address.latitude),
    longitude: Number(values.address.longitude),
  },
  scheduledDate: values.scheduledDate,
  assigneeId: values.assigneeId,
  customerId: values.customerId,
});

export function useCreateJob() {
  const [isOpen, setOpen] = useState(false);
  const [form, dispatch] = useReducer(createJobReducer, INITIAL);
  const [isPending, startTransition] = useTransition();

  const open = useCallback(() => setOpen(true), []);

  const close = useCallback(() => {
    setOpen(false);
    dispatch({ type: 'RESET' });
  }, []);

  const change = useCallback(
    (field: CreateJobField, value: string) => dispatch({ type: 'FIELD_CHANGED', field, value }),
    [],
  );

  const blur = useCallback(
    (field: CreateJobField) => dispatch({ type: 'FIELD_BLURRED', field }),
    [],
  );

  const submit = useCallback(() => {
    const errors = validate(form.values);
    if (Object.keys(errors).length > 0) {
      dispatch({
        type: 'SUBMIT_FAILED',
        formError: 'Fix the highlighted fields',
        fieldErrors: errors,
      });
      return;
    }

    dispatch({ type: 'SUBMIT_STARTED' });

    startTransition(async () => {
      const outcome = await createJobAction(toInput(form.values));

      if (outcome.ok) {
        dispatch({ type: 'SUBMIT_SUCCEEDED' });
        setOpen(false);
        jobsEventBus.emit('jobs:invalidate', undefined);
        return;
      }

      dispatch({
        type: 'SUBMIT_FAILED',
        formError: outcome.error.message,
        fieldErrors: outcome.error.fieldErrors,
      });
    });
  }, [form.values]);

  return { isOpen, open, close, form, change, blur, submit, isPending };
}
