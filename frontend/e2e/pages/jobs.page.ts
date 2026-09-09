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
}
