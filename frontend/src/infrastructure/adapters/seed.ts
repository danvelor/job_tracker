import type { JobStatus } from '@/core/domain/job/job-status.type';
import type { Party } from '@/core/domain/job/job-summary.type';

/**
 * The roster ids match what plan 3 seeds into `jobs.assignees` and
 * `jobs.customers`, so the two adapters agree on what the pickers offer.
 */
export const SEED_ASSIGNEES: readonly Party[] = [
  { id: 'assignee-1', name: 'J. Ortiz' },
  { id: 'assignee-2', name: 'M. Ruiz' },
];

export const SEED_CUSTOMERS: readonly Party[] = [
  { id: 'customer-1', name: 'Acme Holdings' },
  { id: 'customer-2', name: 'Birch Property' },
];

export type SeedPhoto = {
  id: string;
  url: string;
  capturedAt: string;
  caption: string | null;
};

export type SeedJob = {
  id: string;
  title: string;
  description: string;
  status: JobStatus;
  scheduledDate: string;
  assigneeId: string;
  customerId: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: number;
    longitude: number;
  };
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  signatureUrl: string | null;
  photos: SeedPhoto[];
};

const address = (street: string) => ({
  street,
  city: 'Springfield',
  state: 'IL',
  zipCode: '62701',
  latitude: 39.78,
  longitude: -89.65,
});

/**
 * Scheduled dates sit in 2099 so BR-1 never starts rejecting a seeded job as
 * the real clock advances. A fixture that expires is a test that fails for a
 * reason unrelated to the code.
 *
 * Returns a fresh array each call, so one adapter instance cannot see another's
 * writes.
 */
export const seedJobs = (): SeedJob[] => [
  {
    id: 'job-1',
    title: 'Ridge tile replacement',
    description: 'Replace cracked ridge tiles on the north slope',
    status: 'Scheduled',
    scheduledDate: '2099-03-14',
    assigneeId: 'assignee-1',
    customerId: 'customer-1',
    address: address('12 Elm St'),
    startedAt: null,
    completedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: null,
    photos: [],
  },
  {
    id: 'job-2',
    title: 'Gutter reline',
    description: 'Reline the rear gutter run',
    status: 'InProgress',
    scheduledDate: '2099-03-13',
    assigneeId: 'assignee-2',
    customerId: 'customer-2',
    address: address('8 Oak Ave'),
    startedAt: '2099-03-13T09:00:00.000Z',
    completedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: null,
    photos: [],
  },
  {
    id: 'job-3',
    title: 'Shingle swap',
    description: 'Swap storm-damaged shingles',
    status: 'Completed',
    scheduledDate: '2099-03-09',
    assigneeId: 'assignee-1',
    customerId: 'customer-1',
    address: address('44 Pine Rd'),
    startedAt: '2099-03-09T08:00:00.000Z',
    completedAt: '2099-03-09T15:30:00.000Z',
    cancelledAt: null,
    cancellationReason: null,
    signatureUrl: 'data:image/png;base64,seed',
    photos: [
      {
        id: 'photo-1',
        url: 'https://example.invalid/photo-1.jpg',
        capturedAt: '2099-03-09T14:00:00.000Z',
        caption: 'After',
      },
    ],
  },
  {
    id: 'job-4',
    title: 'Flashing inspection',
    description: 'Inspect chimney flashing after a leak report',
    status: 'Cancelled',
    scheduledDate: '2099-03-08',
    assigneeId: 'assignee-2',
    customerId: 'customer-2',
    address: address('3 Cedar Ln'),
    startedAt: null,
    completedAt: null,
    cancelledAt: '2099-03-07T12:00:00.000Z',
    cancellationReason: 'Customer withdrew',
    signatureUrl: null,
    photos: [],
  },
];
