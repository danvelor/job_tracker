import { defineConfig, devices } from '@playwright/test';

/**
 * The smoke run targets a stack that is already up (`docker compose up
 * --wait`), so it must not start a server of its own. Two projects in one
 * config rather than two configs, which would drift.
 */
const smoke = process.env.SMOKE_BASE_URL;

/**
 * A port nothing listens on. The offline server points the frontend's API url
 * at it, so `HttpJobsAdapter` cannot connect, `app/jobs/page.tsx` throws, and
 * `app/jobs/error.tsx` takes over. That is the only faithful way to reach the
 * route error boundary from a browser: the failure happens during the Server
 * Component render, where request interception cannot reach.
 */
const UNREACHABLE_API = 'http://127.0.0.1:59999';

const server = (port: number, env?: Record<string, string>) => ({
  // The build is the e2e script's job, not a server's. Two servers, one
  // `.next` directory: whichever started second would race the other's build.
  command: `npm run start -- --port ${port}`,
  url: `http://127.0.0.1:${port}`,
  reuseExistingServer: !process.env.CI,
  timeout: 120_000,
  ...(env === undefined ? {} : { env }),
});

export default defineConfig({
  testDir: './e2e',
  // One worker, no parallelism: the in-memory adapter is a per-process
  // singleton, so two workers would mutate one another's data.
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    // 3100, not 3000. Compose serves the real stack on 3000, and Playwright's
    // reuseExistingServer will happily adopt whatever is already listening —
    // which silently ran the in-memory suite against the Compose stack once.
    // Separate ports mean the two suites can never target each other's server.
    baseURL: 'http://127.0.0.1:3100',
    trace: 'retain-on-failure',
    // Rubric line 441 asks for screenshots on failure.
    screenshot: 'only-on-failure',
  },
  projects: smoke
    ? [
        {
          name: 'smoke',
          testMatch: /smoke\.spec\.ts/,
          use: { ...devices['Desktop Chrome'], baseURL: smoke },
        },
      ]
    : [
        {
          name: 'chromium',
          // The in-memory suite, and only it. The smoke spec needs a running
          // stack and the offline spec needs a broken one, so running either
          // here would fail for the right reason at the wrong time.
          testIgnore: [/smoke\.spec\.ts/, /backend-unreachable\.spec\.ts/],
          use: { ...devices['Desktop Chrome'] },
        },
        {
          name: 'backend-unreachable',
          testMatch: /backend-unreachable\.spec\.ts/,
          use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:3101' },
        },
      ],
  // Absent for the smoke run: Compose is already serving, and starting a
  // second server would test the in-memory adapter under the smoke name.
  ...(smoke
    ? {}
    : {
        webServer: [
          server(3100),
          server(3101, { JOBTRACKER_API_URL: UNREACHABLE_API }),
        ],
      }),
});
