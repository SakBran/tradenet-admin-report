/**
 * The one place that posts an Excel export job and handles the queue response.
 *
 * Every report page (generic and bespoke) goes through this so the presentation
 * spec always rides along, and so the Ready / Queued / Processing handling and
 * the "spec rejected — reload the page" recovery are identical everywhere.
 */
import axios from 'axios';
import { message } from 'antd';

import axiosInstance from '../../services/AxiosInstance';
import { ExcelEnqueueResult, ExcelPresentationSpec } from './excelTypes';

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/**
 * The backend refused the presentation spec (HTTP 400 from
 * `RequireExcelPresentationSpecFilter`): the running bundle is older than the
 * API. Only a reload can fix it, so the user is told to reload.
 */
export class ExcelSpecRejectedError extends Error {
  readonly errors: string[];

  constructor(errors: string[]) {
    super(
      errors.length
        ? `Excel export rejected: ${errors.join('; ')}`
        : 'Excel export rejected: the report layout this page sent is out of date.'
    );
    this.name = 'ExcelSpecRejectedError';
    this.errors = errors;
  }
}

const downloadBlob = (blob: Blob, fileName: string) => {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

const rejectionErrors = (data: unknown): string[] => {
  if (!data || typeof data !== 'object') {
    return [];
  }

  const payload = data as { errors?: unknown; message?: unknown; title?: unknown };

  if (Array.isArray(payload.errors)) {
    return payload.errors.map((entry) => String(entry));
  }

  if (payload.errors && typeof payload.errors === 'object') {
    return Object.values(payload.errors as Record<string, unknown>).flatMap(
      (entry) => (Array.isArray(entry) ? entry.map(String) : [String(entry)])
    );
  }

  const single = payload.message ?? payload.title;
  return single ? [String(single)] : [];
};

/** One row of `GET ExcelExport/{id}`, the subset the wait loop reads. */
interface ExcelJobStatus {
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  fileName?: string | null;
  downloadUrl?: string | null;
  errorMessage?: string | null;
}

/**
 * How long to follow a queued job before handing the user back to the Exports page.
 * Most reports finish in a second or two (the worker polls, so there is a small fixed
 * pickup delay); a genuinely large export outlives this budget and is collected from
 * Exports, exactly as before.
 */
const POLL_INTERVAL_MS = 1_000;
const POLL_BUDGET_MS = 60_000;

const delay = (ms: number) =>
  new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });

const download = async (url: string, fileName: string) => {
  const fileResponse = await axiosInstance.get(url, { responseType: 'blob' });
  const blob = new Blob([fileResponse.data], {
    type: String(fileResponse.headers['content-type'] ?? excelContentType),
  });
  downloadBlob(blob, fileName);
};

/**
 * Follows a queued/processing job to completion and downloads it.
 *
 * Without this the button only ever said "queued": nothing polled, so on every export
 * of an open period (and on every first export of any period) the user was silently
 * handed off to the Exports page and read that as "the report cannot be exported".
 *
 * @returns whether the outcome was reported to the user. Never throws: the job is
 *   already queued at this point, and BasicTable turns a throw into "Failed to generate
 *   Excel file" — so a blip while polling must not report a successful export as failed.
 *   Anything unresolved falls through to the caller's "it will appear in Exports" message.
 */
const waitForJob = async (
  jobId: string,
  fallbackFileName: string
): Promise<boolean> => {
  const deadline = Date.now() + POLL_BUDGET_MS;

  while (Date.now() < deadline) {
    await delay(POLL_INTERVAL_MS);

    let job: ExcelJobStatus;
    try {
      const { data } = await axiosInstance.get<ExcelJobStatus>(
        `ExcelExport/${jobId}`
      );
      job = data;
    } catch {
      // Keep polling: one failed status read says nothing about the job.
      continue;
    }

    if (job.status === 'Completed' && job.downloadUrl) {
      try {
        await download(job.downloadUrl, job.fileName ?? fallbackFileName);
      } catch {
        // The file exists but this download did not land; Exports still has it.
        message.info(
          'Your Excel export is ready. Open Exports to download it.'
        );
        return true;
      }

      message.success('Your Excel export is ready and downloading.');
      return true;
    }

    if (job.status === 'Failed') {
      message.error(
        job.errorMessage
          ? `Excel export failed: ${job.errorMessage}`
          : 'Excel export failed. Please try again.'
      );
      return true;
    }
  }

  return false;
};

/**
 * Enqueues the export, follows it to completion and reports its outcome to the user.
 *
 * @param route            the report's `excelRoute` (e.g. `MPUReport/Excel`)
 * @param request          the grid request body (filters + paging)
 * @param spec             the presentation spec, posted as `excel`; omit it for a
 *                         controller that builds its own sheet from an
 *                         `IExcelReportLayoutProvider` (the four Total Value pages) —
 *                         `RequireExcelPresentationSpecFilter` only demands a spec from
 *                         controllers that have no typed layout
 * @param fallbackFileName file name to save under when the job does not name one
 * @throws ExcelSpecRejectedError on HTTP 400 (stale bundle); rethrows anything else
 */
export const enqueueExcelExport = async (
  route: string,
  request: Record<string, unknown>,
  spec: ExcelPresentationSpec | undefined,
  fallbackFileName: string
): Promise<void> => {
  let result: ExcelEnqueueResult;

  try {
    const response = await axiosInstance.post<ExcelEnqueueResult>(
      route,
      spec ? { ...request, excel: spec } : request
    );
    result = response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 400) {
      message.error(
        'This report was updated. Please reload the page (Ctrl+F5) and export again.'
      );
      throw new ExcelSpecRejectedError(rejectionErrors(error.response.data));
    }

    throw error;
  }

  if (result.status === 'Ready' && result.downloadUrl) {
    await download(result.downloadUrl, result.fileName ?? fallbackFileName);
    message.success('Your Excel export is ready and downloading.');
    return;
  }

  if (result.jobId && (await waitForJob(result.jobId, fallbackFileName))) {
    return;
  }

  if (result.status === 'Processing') {
    message.info(
      'This export is already being generated. It will appear in Exports when ready.'
    );
    return;
  }

  message.success('Export queued. It will appear in Exports when ready.');
};
