'use client';

import type { Party } from '@/core/domain/job/job-summary.type';
import { Button } from '@/presentation/components/atoms/button.component';
import { DateInput } from '@/presentation/components/atoms/date-input.component';
import { Select } from '@/presentation/components/atoms/select.component';
import { TextInput } from '@/presentation/components/atoms/text-input.component';
import { FormField } from '@/presentation/components/molecules/form-field.component';
import type { CreateJobField, CreateJobFormState } from '../../hooks/use-create-job.hook';

type TextFieldSpec = {
  readonly field: CreateJobField;
  readonly label: string;
  readonly testId: string;
};

const TEXT_FIELDS: readonly TextFieldSpec[] = [
  { field: 'title', label: 'Title', testId: 'create-job-title' },
  { field: 'description', label: 'Description', testId: 'create-job-description' },
  { field: 'address.street', label: 'Street', testId: 'create-job-street' },
  { field: 'address.city', label: 'City', testId: 'create-job-city' },
  { field: 'address.state', label: 'State', testId: 'create-job-state' },
  { field: 'address.zipCode', label: 'ZIP code', testId: 'create-job-zip' },
  { field: 'address.latitude', label: 'Latitude', testId: 'create-job-latitude' },
  { field: 'address.longitude', label: 'Longitude', testId: 'create-job-longitude' },
];

const ADDRESS_PREFIX = 'address.';

type AddressKey = keyof CreateJobFormState['values']['address'];
type TopLevelKey = 'title' | 'description' | 'scheduledDate' | 'assigneeId' | 'customerId';

const valueAt = (form: CreateJobFormState, field: CreateJobField): string =>
  field.startsWith(ADDRESS_PREFIX)
    ? form.values.address[field.slice(ADDRESS_PREFIX.length) as AddressKey]
    : form.values[field as TopLevelKey];

export function CreateJobModal({
  form,
  assignees,
  customers,
  isPending,
  onChange,
  onBlur,
  onSubmit,
  onCancel,
}: {
  readonly form: CreateJobFormState;
  readonly assignees: readonly Party[];
  readonly customers: readonly Party[];
  readonly isPending: boolean;
  readonly onChange: (field: CreateJobField, value: string) => void;
  readonly onBlur: (field: CreateJobField) => void;
  readonly onSubmit: () => void;
  readonly onCancel: () => void;
}) {
  return (
    <div
      data-testid="create-job-modal"
      role="dialog"
      aria-modal="true"
      aria-label="New job"
      className="fixed inset-0 z-10 flex items-start justify-center overflow-y-auto bg-black/30 p-8"
    >
      <div className="w-full max-w-lg rounded bg-white p-6">
        <h2 className="mb-4 text-lg font-semibold">New job</h2>

        {TEXT_FIELDS.map(({ field, label, testId }) => (
          <FormField key={field} id={testId} label={label} error={form.errors[field]}>
            <TextInput
              testId={testId}
              id={testId}
              value={valueAt(form, field)}
              onChange={(value) => onChange(field, value)}
              onBlur={() => onBlur(field)}
            />
          </FormField>
        ))}

        <FormField
          id="create-job-scheduled-date"
          label="Scheduled date"
          error={form.errors.scheduledDate}
        >
          <DateInput
            testId="create-job-scheduled-date"
            id="create-job-scheduled-date"
            value={form.values.scheduledDate}
            onChange={(value) => onChange('scheduledDate', value)}
          />
        </FormField>

        <FormField id="create-job-assignee" label="Crew" error={form.errors.assigneeId}>
          <Select
            testId="create-job-assignee"
            id="create-job-assignee"
            value={form.values.assigneeId}
            placeholder="Choose crew"
            options={assignees.map((party) => ({ value: party.id, label: party.name }))}
            onChange={(value) => onChange('assigneeId', value)}
          />
        </FormField>

        <FormField id="create-job-customer" label="Customer" error={form.errors.customerId}>
          <Select
            testId="create-job-customer"
            id="create-job-customer"
            value={form.values.customerId}
            placeholder="Choose customer"
            options={customers.map((party) => ({ value: party.id, label: party.name }))}
            onChange={(value) => onChange('customerId', value)}
          />
        </FormField>

        {form.formError === null ? null : (
          <p data-testid="create-job-error" role="alert" className="mb-3 text-sm text-red-700">
            {form.formError}
          </p>
        )}

        <div className="flex gap-2">
          <Button testId="create-job-submit" onClick={onSubmit} pending={isPending}>
            Create job
          </Button>
          <Button testId="create-job-cancel" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}
