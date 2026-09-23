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
const error = vi.fn();

vi.mock('antd', () => ({
  message: {
    success: (...args: unknown[]) => success(...args),
    error: (...args: unknown[]) => error(...args),
  },
}));

// Following the job is the watcher's business (excelJobWatcher.test.ts); here we only
// check what the enqueue hands it, and that it hands it over instead of waiting.
const watchExcelJob = vi.fn();
const deliverReadyExport = vi.fn();

vi.mock('./excelJobWatcher', () => ({
  watchExcelJob: (...args: unknown[]) => watchExcelJob(...args),
  deliverReadyExport: (...args: unknown[]) => deliverReadyExport(...args),
}));

const { enqueueExcelExport, ExcelSpecRejectedError } = await import(
  './excelEnqueue'
);

const spec = { controllerName: 'MPUReport' } as never;

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * No fake timers anywhere in this file, on purpose: every call below resolves on the POST
 * alone. Were the enqueue still waiting on a poll timer, these tests would hang instead of
 * pass -- that wait is exactly what customers read as "this Excel is not a job".
 */
describe('enqueueExcelExport', () => {
  // The complaint: the button followed the job for up to 60s before letting go.
  it('hands a queued job to the background follower and returns at once', async () => {
    post.mockResolvedValue({
      data: { status: 'Queued', jobId: 'j2', fileName: 'b.xlsx' },
    });

    await enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx');

    expect(watchExcelJob).toHaveBeenCalledWith('j2', 'b.xlsx', {
      alreadyRunning: false,
    });
    expect(get).not.toHaveBeenCalled();
    expect(deliverReadyExport).not.toHaveBeenCalled();
  });

  it('follows the identical export already in flight when the queue says Processing', async () => {
    post.mockResolvedValue({ data: { status: 'Processing', jobId: 'j3' } });

    await enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx');

    expect(watchExcelJob).toHaveBeenCalledWith('j3', 'f.xlsx', {
      alreadyRunning: true,
    });
    expect(get).not.toHaveBeenCalled();
  });

  it('downloads a reused file in the background', async () => {
    post.mockResolvedValue({
      data: {
        status: 'Ready',
        jobId: 'j1',
        downloadUrl: 'd/1',
        fileName: 'a.xlsx',
      },
    });

    await enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx');

    expect(deliverReadyExport).toHaveBeenCalledWith('j1', 'd/1', 'a.xlsx');
    expect(watchExcelJob).not.toHaveBeenCalled();
    expect(get).not.toHaveBeenCalled();
  });

  it('points the user at Exports if a response ever comes back without a job id', async () => {
    post.mockResolvedValue({ data: { status: 'Queued' } });

    await enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx');

    expect(success).toHaveBeenCalledWith(
      'Export queued. It will appear in Exports when ready.'
    );
    expect(watchExcelJob).not.toHaveBeenCalled();
  });

  // The four Total Value pages: their controllers build the sheet from a typed layout,
  // so they post no spec and the backend must not be sent an `excel` key at all.
  it('omits the excel key entirely when no spec is given', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j6' } });

    await enqueueExcelExport(
      'ImportLicenceTotalValueLicencesReport/Excel',
      { FromDate: '2025-01-01' },
      undefined,
      'f.xlsx'
    );

    expect(post).toHaveBeenCalledWith(
      'ImportLicenceTotalValueLicencesReport/Excel',
      { FromDate: '2025-01-01' }
    );
  });

  it('posts the presentation spec as `excel` alongside the request', async () => {
    post.mockResolvedValue({ data: { status: 'Queued', jobId: 'j7' } });

    await enqueueExcelExport(
      'MPUReport/Excel',
      { FromDate: 'x' },
      spec,
      'f.xlsx'
    );

    expect(post).toHaveBeenCalledWith('MPUReport/Excel', {
      FromDate: 'x',
      excel: spec,
    });
  });

  it('surfaces a rejected spec as ExcelSpecRejectedError', async () => {
    post.mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { errors: ['excel.columns: required'] } },
    });

    await expect(
      enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx')
    ).rejects.toBeInstanceOf(ExcelSpecRejectedError);

    expect(error).toHaveBeenCalled();
    expect(watchExcelJob).not.toHaveBeenCalled();
  });

  // BasicTable turns a throw into "Failed to generate Excel file": a POST that never
  // reached the queue is a real failure and must still say so.
  it('rethrows any other failure to enqueue', async () => {
    const serverError = {
      isAxiosError: true,
      response: { status: 500, data: {} },
    };
    post.mockRejectedValue(serverError);

    await expect(
      enqueueExcelExport('MPUReport/Excel', {}, spec, 'f.xlsx')
    ).rejects.toBe(serverError);

    expect(watchExcelJob).not.toHaveBeenCalled();
    expect(error).not.toHaveBeenCalled();
  });
});
