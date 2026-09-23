import { beforeEach, describe, expect, it, vi } from 'vitest';

const get = vi.fn();

vi.mock('../../services/AxiosInstance', () => ({
  default: { get: (...args: unknown[]) => get(...args) },
}));

const open = vi.fn();
const success = vi.fn();
const info = vi.fn();
const warning = vi.fn();
const error = vi.fn();
const destroy = vi.fn();
const messageError = vi.fn();

vi.mock('antd', () => ({
  Button: () => null,
  Space: () => null,
  message: { error: (...args: unknown[]) => messageError(...args) },
  notification: {
    open: (...args: unknown[]) => open(...args),
    success: (...args: unknown[]) => success(...args),
    info: (...args: unknown[]) => info(...args),
    warning: (...args: unknown[]) => warning(...args),
    error: (...args: unknown[]) => error(...args),
    destroy: (...args: unknown[]) => destroy(...args),
  },
}));

vi.mock('@ant-design/icons', () => ({ LoadingOutlined: () => null }));

const { watchExcelJob, deliverReadyExport, setOpenExports } = await import(
  './excelJobWatcher'
);

/** A one-cell xlsx stand-in; the watcher only forwards it to the DOM. */
const blobResponse = () => ({ data: new Uint8Array([1]), headers: {} });

const httpError = (status: number) => ({
  isAxiosError: true,
  response: { status, data: {} },
});

/**
 * The suite runs on the Node environment on purpose (vitest.config.ts keeps it fast and
 * dependency-light), so the handful of browser APIs the watcher touches are stubbed here
 * rather than pulling in jsdom. `clicked` records the file name of every download that
 * fired; `session.token` is what `localStorage.getItem('token')` returns.
 */
const clicked: string[] = [];
const session = { token: 'token-a' as string | null, hidden: false };

const stubBrowser = () => {
  clicked.length = 0;
  session.token = 'token-a';
  session.hidden = false;

  vi.stubGlobal('window', {
    URL: { createObjectURL: () => 'blob:x', revokeObjectURL: () => {} },
  });
  vi.stubGlobal('document', {
    get visibilityState() {
      return session.hidden ? 'hidden' : 'visible';
    },
    createElement: () => ({
      href: '',
      download: '',
      click() {
        clicked.push(String((this as { download: string }).download));
      },
      remove() {},
    }),
    body: { appendChild: () => {} },
  });
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => (key === 'token' ? session.token : null),
  });
};

const statusReads = (jobId: string) =>
  get.mock.calls.filter(([url]) => url === `ExcelExport/${jobId}`).length;

interface ElementLike {
  props: { children?: unknown; onClick?: () => void };
}

interface NoticeArgs {
  key: string;
  message: string;
  description?: string;
  duration?: number;
  onClose?: () => void;
  actions?: ElementLike;
}

const lastArgs = (fn: ReturnType<typeof vi.fn>) =>
  fn.mock.calls[fn.mock.calls.length - 1][0] as NoticeArgs;

/** The buttons inside a notice's `actions` (a Space of Buttons), by label. */
const button = (notice: NoticeArgs, label: string) =>
  ([] as ElementLike[])
    .concat((notice.actions?.props.children ?? []) as ElementLike[])
    .find((child) => child.props.children === label);

beforeEach(() => {
  vi.clearAllMocks();
  vi.useFakeTimers();
  stubBrowser();
});

// Every test uses its own job id: the watcher is module-level (it has to outlive the
// page that started a job), so ids are what keep one test's job out of the next.
describe('watchExcelJob', () => {
  it('shows a progress notice straight away, before anything is polled', () => {
    watchExcelJob('j-notice', 'f.xlsx');

    expect(open).toHaveBeenCalledTimes(1);
    expect(lastArgs(open)).toMatchObject({
      key: 'excel-job-j-notice',
      message: 'Preparing f.xlsx',
      duration: 0,
    });
    expect(lastArgs(open).description).toMatch(/keep working/i);
    expect(get).not.toHaveBeenCalled();
  });

  it('follows a queued job to completion and downloads it', async () => {
    get
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/2', fileName: 'b.xlsx' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-done', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(get).toHaveBeenCalledWith('ExcelExport/j-done');
    expect(get).toHaveBeenCalledWith('d/2', { responseType: 'blob' });
    expect(clicked).toEqual(['b.xlsx']);
    expect(lastArgs(success)).toMatchObject({
      key: 'excel-job-j-done',
      message: 'b.xlsx is ready',
      duration: 15,
    });
    expect(error).not.toHaveBeenCalled();
  });

  // Every poll is an activity-log row; the old loop read once a second for a minute.
  it('polls briskly at first, then backs off', async () => {
    get.mockResolvedValue({ data: { status: 'Processing' } });

    watchExcelJob('j-cadence', 'f.xlsx');

    await vi.advanceTimersByTimeAsync(10_000);
    expect(statusReads('j-cadence')).toBe(10);

    await vi.advanceTimersByTimeAsync(50_000);
    expect(statusReads('j-cadence')).toBe(35);

    await vi.runAllTimersAsync();
  });

  it('updates the progress notice as the job moves from queued to generating', async () => {
    get
      .mockResolvedValueOnce({ data: { status: 'Queued' } })
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/3' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-phases', 'f.xlsx');
    await vi.runAllTimersAsync();

    const descriptions = open.mock.calls.map(
      ([args]) => (args as NoticeArgs).description
    );
    // Opened as queued, re-opened once on the move to Processing (not on every poll),
    // and once more while the finished file downloads.
    expect(descriptions).toHaveLength(3);
    expect(descriptions[0]).toMatch(/waiting for the export worker/i);
    expect(descriptions[1]).toMatch(/generating the file/i);
    expect(descriptions[2]).toMatch(/downloading/i);
  });

  // The worker puts a failed attempt back in the queue with its error kept; only
  // `Failed` is final.
  it('shows a retry, not a failure, when a failed attempt is requeued', async () => {
    get
      .mockResolvedValueOnce({
        data: { status: 'Queued', errorMessage: 'Timeout expired.' },
      })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/r' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-retry', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(open.mock.calls[1][0].description).toMatch(
      /retrying after an error \(Timeout expired\.\)/i
    );
    expect(error).not.toHaveBeenCalled();
    expect(clicked).toEqual(['f.xlsx']);
  });

  it('stops re-opening the progress notice once the user closes it, but still delivers', async () => {
    get
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/4' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-dismissed', 'f.xlsx');
    lastArgs(open).onClose?.();
    await vi.runAllTimersAsync();

    expect(open).toHaveBeenCalledTimes(1);
    expect(clicked).toEqual(['f.xlsx']);
    expect(success).toHaveBeenCalledTimes(1);
  });

  it("reports the worker's error when the job fails", async () => {
    get.mockResolvedValue({
      data: { status: 'Failed', errorMessage: 'Failed to connect to host.' },
    });

    watchExcelJob('j-failed', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(lastArgs(error)).toMatchObject({
      key: 'excel-job-j-failed',
      message: 'Excel export failed',
      duration: 0,
    });
    expect(lastArgs(error).description).toContain('Failed to connect to host.');
    expect(clicked).toEqual([]);
  });

  it('keeps polling through a failed status read', async () => {
    get
      .mockRejectedValueOnce(new Error('network'))
      .mockRejectedValueOnce(httpError(500))
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/5' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-blip', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(statusReads('j-blip')).toBe(3);
    expect(clicked).toEqual(['f.xlsx']);
    expect(error).not.toHaveBeenCalled();
  });

  it('says so when the job was deleted from Exports before it finished', async () => {
    get.mockRejectedValue(httpError(404));

    watchExcelJob('j-gone', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(statusReads('j-gone')).toBe(1);
    expect(lastArgs(warning)).toMatchObject({ key: 'excel-job-j-gone' });
  });

  // The layout's interceptor signs the user out on a 401; a loop left running would
  // keep hitting the API with a dead token.
  it('stops when the session has expired, and says where the file will be', async () => {
    get.mockRejectedValue(httpError(401));

    watchExcelJob('j-401', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(statusReads('j-401')).toBe(1);
    expect(lastArgs(info)).toMatchObject({ key: 'excel-job-j-401' });
    expect(lastArgs(info).description).toMatch(/Exports/);
    expect(error).not.toHaveBeenCalled();
  });

  // Signed out, or another user signed in on the same browser: not theirs to receive.
  it('stops quietly when the signed-in session changes', async () => {
    get.mockResolvedValue({ data: { status: 'Processing' } });

    watchExcelJob('j-switch', 'f.xlsx');
    await vi.advanceTimersByTimeAsync(3_000);
    const readsBefore = statusReads('j-switch');

    session.token = 'token-b';
    await vi.runAllTimersAsync();

    expect(statusReads('j-switch')).toBe(readsBefore);
    expect(destroy).toHaveBeenCalledWith('excel-job-j-switch');
    expect(clicked).toEqual([]);
  });

  it('hands off to Exports when the job outlives the follow budget', async () => {
    const openExports = vi.fn();
    setOpenExports(openExports);
    get.mockResolvedValue({ data: { status: 'Processing' } });

    watchExcelJob('j-slow', 'f.xlsx');
    await vi.runAllTimersAsync();

    // Thirty minutes of backed-off polling, then it lets go (no endless loop).
    expect(statusReads('j-slow')).toBe(183);
    const notice = lastArgs(info);
    expect(notice).toMatchObject({
      key: 'excel-job-j-slow',
      message: 'f.xlsx is still being generated',
      duration: 0,
    });
    button(notice, 'Open Exports')?.props.onClick?.();
    expect(openExports).toHaveBeenCalledTimes(1);
    expect(clicked).toEqual([]);
  });

  // A repeat click on the same filters comes back from the enqueue as `Processing` with
  // the SAME job id: one loop, one download.
  it('re-shows an already-followed job instead of polling it twice', async () => {
    get
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/6' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-twice', 'f.xlsx');
    watchExcelJob('j-twice', 'f.xlsx', { alreadyRunning: true });
    await vi.runAllTimersAsync();

    expect(statusReads('j-twice')).toBe(2);
    expect(clicked).toEqual(['f.xlsx']);
    expect(success).toHaveBeenCalledTimes(1);
  });

  it('says an identical export was already requested when it did not create the job', () => {
    watchExcelJob('j-twin', 'f.xlsx', { alreadyRunning: true });

    expect(lastArgs(open).description).toMatch(/already requested/i);
  });

  // A download fired long after the click has no user gesture, so a browser may block it.
  // The notice's Download must save with no await, so its own click is the gesture.
  it("saves the kept file synchronously from the success notice's Download button", async () => {
    get
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/7' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-regesture', 'f.xlsx');
    await vi.advanceTimersByTimeAsync(1_000);

    expect(clicked).toEqual(['f.xlsx']);
    const readsBefore = get.mock.calls.length;

    button(lastArgs(success), 'Download')?.props.onClick?.();

    expect(clicked).toEqual(['f.xlsx', 'f.xlsx']);
    expect(get.mock.calls.length).toBe(readsBefore);

    await vi.runAllTimersAsync();
  });

  it('keeps the success notice open when the job finished while the tab was hidden', async () => {
    session.hidden = true;
    get
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/8' },
      })
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-hidden', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(lastArgs(success).duration).toBe(0);
  });

  it('offers Download and Open Exports when the automatic download fails', async () => {
    setOpenExports(vi.fn());
    get
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/9' },
      })
      .mockRejectedValueOnce(new Error('blob failed'))
      .mockResolvedValue(blobResponse());

    watchExcelJob('j-blocked', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(clicked).toEqual([]);
    const notice = lastArgs(info);
    expect(notice).toMatchObject({ key: 'excel-job-j-blocked', duration: 0 });
    expect(button(notice, 'Open Exports')).toBeDefined();

    button(notice, 'Download')?.props.onClick?.();
    await vi.runAllTimersAsync();

    expect(clicked).toEqual(['f.xlsx']);
    expect(messageError).not.toHaveBeenCalled();
  });

  it('says the file is gone when it expired before it could be downloaded', async () => {
    get
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/10' },
      })
      .mockRejectedValueOnce(httpError(410));

    watchExcelJob('j-expired', 'f.xlsx');
    await vi.runAllTimersAsync();

    expect(lastArgs(warning)).toMatchObject({ key: 'excel-job-j-expired' });
    expect(info).not.toHaveBeenCalled();
    expect(clicked).toEqual([]);
  });
});

describe('deliverReadyExport', () => {
  it('downloads a reused file in the background and reports it', async () => {
    get.mockResolvedValue(blobResponse());

    deliverReadyExport('j-ready', 'd/11', 'r.xlsx');

    expect(lastArgs(open)).toMatchObject({ key: 'excel-job-j-ready' });
    expect(lastArgs(open).description).toMatch(/downloading/i);

    await vi.runAllTimersAsync();

    expect(get).toHaveBeenCalledWith('d/11', { responseType: 'blob' });
    expect(clicked).toEqual(['r.xlsx']);
    expect(lastArgs(success)).toMatchObject({
      key: 'excel-job-j-ready',
      message: 'r.xlsx is ready',
    });
  });
});
