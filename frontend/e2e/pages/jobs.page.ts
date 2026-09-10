import type { Locator, Page, Response } from '@playwright/test';

/**
 * Every selector comes from the data-testid contract in design A8. No CSS
 * class and no visible text, so a copy change cannot break the suite.
 */
export class JobsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/jobs');
  }

  /**
   * Returns the response so a caller can assert the status code. A custom 404
   * that renders while the server answered 200 would look right in the browser
   * and be wrong for every crawler and every client that reads the status.
   */
  async gotoJob(id: string): Promise<Response | null> {
    return this.page.goto(`/jobs/${id}`);
  }

  get root(): Locator {
    return this.page.getByTestId('jobs-page');
  }

  get table(): Locator {
    return this.page.getByTestId('jobs-table');
  }

  get skeleton(): Locator {
    return this.page.getByTestId('jobs-table-skeleton');
  }

  /** The route transition's skeleton, a different event (design A8). */
  get routeSkeleton(): Locator {
    return this.page.getByTestId('jobs-route-skeleton');
  }

  get emptyNoMatches(): Locator {
    return this.page.getByTestId('jobs-empty-no-matches');
  }

  get emptyNoJobs(): Locator {
    return this.page.getByTestId('jobs-empty-no-jobs');
  }

  get selectionSummary(): Locator {
    return this.page.getByTestId('jobs-selection-summary');
  }

  get statusFilter(): Locator {
    return this.page.getByTestId('filter-status-select');
  }

  statusOption(status: string): Locator {
    return this.page.getByTestId(`filter-status-option-${status}`);
  }

  get searchInput(): Locator {
    return this.page.getByTestId('filter-search-input');
  }

  get clearFilters(): Locator {
    return this.page.getByTestId('filter-clear-button');
  }

  row(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}`);
  }

  rowStatus(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-status`);
  }

  rowTitle(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-title`);
  }

  get rows(): Locator {
    // Row testids are job-row-{id}; anchoring on the title cell keeps this to
    // one locator per row rather than one per row-scoped element.
    return this.page.locator('[data-testid^="job-row-"][data-testid$="-title"]');
  }

  /**
   * Waits on the skeletons leaving rather than on a fixed timeout (A8). Both of
   * them: the route transition's and the list's are different events, and
   * against a real backend they are far enough apart to see.
   */
  async waitForList(): Promise<void> {
    await this.routeSkeleton.waitFor({ state: 'detached' });
    await this.skeleton.waitFor({ state: 'detached' });

    // The table or the empty state: the page has settled either way. The
    // in-memory adapter always has seeded rows, so waiting only for the table
    // worked there and hung against a freshly migrated database — where an
    // empty list is the correct first thing a reviewer sees.
    await this.page
      .locator('[data-testid="jobs-table"], [data-testid="jobs-empty-no-jobs"]')
      .first()
      .waitFor({ state: 'visible' });
  }

  get newJobButton(): Locator {
    return this.page.getByTestId('jobs-new-button');
  }

  get createModal(): Locator {
    return this.page.getByTestId('create-job-modal');
  }

  createField(name: string): Locator {
    return this.page.getByTestId(`create-job-${name}`);
  }

  get createSubmit(): Locator {
    return this.page.getByTestId('create-job-submit');
  }

  rowStart(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-start`);
  }

  rowComplete(id: string): Locator {
    return this.page.getByTestId(`job-row-${id}-complete`);
  }

  get completeModal(): Locator {
    return this.page.getByTestId('complete-job-modal');
  }

  get completeSignature(): Locator {
    return this.page.getByTestId('complete-job-signature');
  }

  get completeSubmit(): Locator {
    return this.page.getByTestId('complete-job-submit');
  }

  get completeError(): Locator {
    return this.page.getByTestId('complete-job-error');
  }

  /** Fills the create form with a valid job and submits it. */
  async createJob(title: string): Promise<void> {
    await this.newJobButton.click();
    await this.createModal.waitFor({ state: 'visible' });
    await this.createField('title').fill(title);
    await this.createField('street').fill('99 Birch Way');
    await this.createField('city').fill('Springfield');
    await this.createField('state').fill('IL');
    await this.createField('zip').fill('62701');
    await this.createField('latitude').fill('39.78');
    await this.createField('longitude').fill('-89.65');
    await this.createField('scheduled-date').fill('2099-06-01');
    // By label, not by value. The in-memory adapter's identifiers are
    // 'assignee-1'; the real backend's are GUIDs from the seed migration. The
    // names are the same on both sides, which is what lets one page object
    // drive both suites.
    await this.createField('assignee').selectOption({ label: 'J. Ortiz' });
    await this.createField('customer').selectOption({ label: 'Acme Holdings' });
    await this.createSubmit.click();
    await this.createModal.waitFor({ state: 'detached' });
  }

  /** The title cell whose text matches, from which a row id is read. */
  titleCell(title: string): Locator {
    return this.page
      .locator('[data-testid^="job-row-"][data-testid$="-title"]')
      .filter({ hasText: title });
  }

  /** app/jobs/not-found.tsx, reached through notFound() in [id]/page.tsx. */
  get notFound(): Locator {
    return this.page.getByTestId('jobs-not-found');
  }

  get backToList(): Locator {
    return this.page.getByTestId('jobs-not-found-back');
  }

  /**
   * app/jobs/error.tsx. Anchored on the retry button rather than on the
   * `jobs-error` region: JobsErrorBoundary claims that same testid for the
   * in-page table failure, so the region alone does not say which of the two
   * took over. The retry belongs to the route boundary only.
   */
  get routeErrorRetry(): Locator {
    return this.page.getByTestId('jobs-error-retry');
  }

  get errorRegion(): Locator {
    return this.page.getByTestId('jobs-error');
  }

  /**
   * Reads a row's id back out of its testid. This is the one place the suite
   * treats a selector as data, and it works because A8 fixes the format. A
   * data-job-id attribute would be cleaner; adding one purely for the test is
   * the trade the other way, so the choice is left visible here.
   */
  async idOf(title: string): Promise<string> {
    const testId = await this.titleCell(title).getAttribute('data-testid');
    return String(testId).replace('job-row-', '').replace('-title', '');
  }
}
