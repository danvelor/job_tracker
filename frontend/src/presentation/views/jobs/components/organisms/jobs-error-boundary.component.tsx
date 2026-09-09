'use client';

import { Component } from 'react';
import type { ErrorInfo, ReactNode } from 'react';

type Props = { readonly children: ReactNode };
type State = { readonly message: string | null };

/**
 * Wraps the table only (assessment line 158), so a render failure in one table
 * does not blank the page: the header and the filters survive.
 *
 * A class because React has no hook equivalent for componentDidCatch. It is
 * the one component in the view that holds state, and it is not an organism in
 * the design A3 sense — it renders no domain markup.
 */
export class JobsErrorBoundary extends Component<Props, State> {
  override state: State = { message: null };

  static getDerivedStateFromError(error: Error): State {
    return { message: error.message };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('JobsErrorBoundary caught a render failure', error, info);
  }

  override render(): ReactNode {
    return this.state.message === null ? (
      this.props.children
    ) : (
      <div data-testid="jobs-error" role="alert" className="p-6 text-sm text-red-700">
        The job list could not be displayed.
      </div>
    );
  }
}
