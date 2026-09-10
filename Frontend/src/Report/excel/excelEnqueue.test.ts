import { beforeEach, describe, expect, it, vi } from 'vitest';

const get = vi.fn();
const post = vi.fn();

vi.mock('../../services/AxiosInstance', () => ({
  default: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
  },
}));

const success = vi.fn();
const info = vi.fn();
const error = vi.fn();

vi.mock('antd', () => ({
  message: {
    success: (...args: unknown[]) => success(...args),
    info: (...args: unknown[]) => info(...args),
    error: (...args: unknown[]) => error(...args),
  },
}));

const { enqueueExcelExport, ExcelSpecRejectedError } = await import(
  './excelEnqueue'
);

/** A one-cell xlsx stand-in; the helper only forwards it to the DOM. */
const blobResponse = () => ({ data: new Uint8Array([1]), headers: {} });

const spec = { controllerName: 'MPUReport' } as never;

/**
 * The suite runs on the Node environment on purpose (vitest.config.ts keeps it fast and
 * dependency-light), so the handful of DOM calls `downloadBlob` makes are stubbed here
 * rather than pulling in jsdom. What is under test is the queue handling, not the anchor.
 */
const clicked: string[] = [];

const stubDom = () => {
  clicked.length = 0;

  Object.assign(globalThis, {
    window: { URL: { createObjectURL: () => 'blob:x', revokeObjectURL: () => {} } },
    document: {
      createElement: () => ({
        href: '',
        download: '',
        click() {
          clicked.push(String((this as { download: string }).download));
        },
        remove() {},
      }),
      body: { appendChild: () => {} },
    },
  });
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.useFakeTimers();
  stubDom();
});

/**
 * Runs `work` while letting the poll loop's timers fire, re-raising its rejection only
 * once the timers are drained — attaching the handler up front so a rejection that
 * happens mid-drain is never reported as unhandled.
 */
const settle = async <T,>(work: Promise<T>): Promise<T> => {
  const outcome = work.then(
    (value) => () => value,
    (reason: unknown) => () => {
      throw reason;
    }
  );

  await vi.runAllTimersAsync();
  return (await outcome)();
};

describe('enqueueExcelExport', () => {
  it('downloads immediately when the queue reuses a finished file', async () => {
    post.mockResolvedValue({
      data: { status: 'Ready', jobId: 'j1', downloadUrl: 'd/1', fileName: 'a.xlsx' },
    });
    get.mockResolvedValue(blobResponse());

    await settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'));

    expect(get).toHaveBeenCalledWith('d/1', { responseType: 'blob' });
    expect(success).toHaveBeenCalledWith(
      'Your Excel export is ready and downloading.'
    );
  });

  // The complaint: the button used to stop at "queued" and never hand over a file.
  it('follows a queued job to completion and downloads it', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j2' } });
    get
      .mockResolvedValueOnce({ data: { status: 'Processing' } })
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/2', fileName: 'b.xlsx' },
      })
      .mockResolvedValue(blobResponse());

    await settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'));

    expect(get).toHaveBeenCalledWith('ExcelExport/j2');
    expect(get).toHaveBeenCalledWith('d/2', { responseType: 'blob' });
    expect(success).toHaveBeenCalledWith(
      'Your Excel export is ready and downloading.'
    );
    expect(info).not.toHaveBeenCalled();
  });

  it("reports the worker's error when the job fails", async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j3' } });
    get.mockResolvedValue({
      data: { status: 'Failed', errorMessage: 'Failed to connect to host.' },
    });

    await settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'));

    expect(error).toHaveBeenCalledWith(
      'Excel export failed: Failed to connect to host.'
    );
  });

  // BasicTable turns a throw into "Failed to generate Excel file", so a blip while
  // polling must not report an export that is actually fine as failed.
  it('keeps polling through a failed status read and never throws', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j4' } });
    get
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce({
        data: { status: 'Completed', downloadUrl: 'd/4' },
      })
      .mockResolvedValue(blobResponse());

    await settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'));

    expect(success).toHaveBeenCalledWith(
      'Your Excel export is ready and downloading.'
    );
    expect(error).not.toHaveBeenCalled();
  });

  it('falls back to the Exports message when the job outlives the poll budget', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j5' } });
    get.mockResolvedValue({ data: { status: 'Processing' } });

    await settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'));

    expect(success).toHaveBeenCalledWith(
      'Export queued. It will appear in Exports when ready.'
    );
    expect(error).not.toHaveBeenCalled();
  });

  // The four Total Value pages: their controllers build the sheet from a typed layout,
  // so they post no spec and the backend must not be sent an `excel` key at all.
  it('omits the excel key entirely when no spec is given', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j6' } });
    get.mockResolvedValue({ data: { status: 'Processing' } });

    await settle(
      enqueueExcelExport(
        'ImportLicenceTotalValueLicencesReport/Excel',
        { FromDate: '2025-01-01' },
        undefined,
        'f.xlsx'
      )
    );

    expect(post).toHaveBeenCalledWith(
      'ImportLicenceTotalValueLicencesReport/Excel',
      { FromDate: '2025-01-01' }
    );
  });

  it('surfaces a rejected spec as ExcelSpecRejectedError', async () => {
    post.mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { errors: ['excel.columns: required'] } },
    });

    await expect(
      settle(enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx'))
    ).rejects.toBeInstanceOf(ExcelSpecRejectedError);

    expect(error).toHaveBeenCalled();
    expect(get).not.toHaveBeenCalled();
  });
});
