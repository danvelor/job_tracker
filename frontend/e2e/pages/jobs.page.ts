import type { Locator, Page, Response } from '@playwright/test';

export class JobsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/jobs');
  }

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
    return this.page.locator('[data-testid^="job-row-"][data-testid$="-title"]');
  }

  async waitForList(): Promise<void> {
    await this.routeSkeleton.waitFor({ state: 'detached' });
    await this.skeleton.waitFor({ state: 'detached' });

    await this.page
      .locator('[data-testid="jobs-table"], [data-testid="jobs-empty-no-jobs"]')
      .first()
      .waitFor({ state: 'visible' });
  }

  get loadMore(): Locator {
    return this.page.getByTestId('jobs-load-more');
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
    await this.createField('assignee').selectOption({ label: 'J. Ortiz' });
    await this.createField('customer').selectOption({ label: 'Acme Holdings' });
    await this.createSubmit.click();
    await this.createModal.waitFor({ state: 'detached' });
  }

  titleCell(title: string): Locator {
    return this.page
      .locator('[data-testid^="job-row-"][data-testid$="-title"]')
      .filter({ hasText: title });
  }

  get notFound(): Locator {
    return this.page.getByTestId('jobs-not-found');
  }

  get backToList(): Locator {
    return this.page.getByTestId('jobs-not-found-back');
  }

  get routeErrorRetry(): Locator {
    return this.page.getByTestId('jobs-error-retry');
  }

  get errorRegion(): Locator {
    return this.page.getByTestId('jobs-error');
  }

  async idOf(title: string): Promise<string> {
    const testId = await this.titleCell(title).getAttribute('data-testid');
    return String(testId).replace('job-row-', '').replace('-title', '');
  }
}
