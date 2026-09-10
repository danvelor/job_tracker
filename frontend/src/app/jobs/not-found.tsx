import Link from 'next/link';

export default function JobNotFound() {
  return (
    <section data-testid="jobs-not-found" className="p-6">
      <h1 className="mb-2 text-lg font-semibold">No such job</h1>
      <p className="mb-4 text-sm text-slate-600">
        That job does not exist, or it belongs to another organization.
      </p>
      <Link href="/jobs" data-testid="jobs-not-found-back" className="text-sm underline">
        Back to the job list
      </Link>
    </section>
  );
}
