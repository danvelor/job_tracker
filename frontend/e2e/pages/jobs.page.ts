import type { Locator, Page } from '@playwright/test';

/**
 * Every selector comes from the data-testid contract in design A8. No CSS
 * class and no visible text, so a copy change cannot break the suite.
 */
export class JobsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/jobs');
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

  get emptyNoMatches(): Locator {
    return this.page.getByTestId('jobs-empty-no-matches');
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

  /** Waits on the skeleton leaving rather than on a fixed timeout (A8). */
  async waitForList(): Promise<void> {
    await this.skeleton.waitFor({ state: 'detached' });
    await this.table.waitFor({ state: 'visible' });
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
    await this.createField('assignee').selectOption('assignee-1');
    await this.createField('customer').selectOption('customer-1');
    await this.createSubmit.click();
    await this.createModal.waitFor({ state: 'detached' });
  }

  /** The title cell whose text matches, from which a row id is read. */
  titleCell(title: string): Locator {
    return this.page
      .locator('[data-testid^="job-row-"][data-testid$="-title"]')
      .filter({ hasText: title });
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
