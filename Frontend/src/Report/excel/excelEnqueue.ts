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
import { deliverReadyExport, watchExcelJob } from './excelJobWatcher';
import { ExcelEnqueueResult, ExcelPresentationSpec } from './excelTypes';

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

/**
 * Enqueues the export and returns as soon as the queue has accepted it.
 *
 * The Excel button used to wait here, following the job for up to 60 seconds: an export
 * that finished inside that window looked synchronous (spinner, then file) and a slower one
 * simply stopped spinning, and customers read both as "this Excel is not a job". The job is
 * now handed to `excelJobWatcher`, which follows it in the background, downloads the file
 * when it is done -- even after the user has moved on to another report -- and reports
 * every outcome in a notification.
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

  const fileName = result.fileName ?? fallbackFileName;

  if (result.status === 'Ready' && result.downloadUrl) {
    deliverReadyExport(result.jobId, result.downloadUrl, fileName);
    return;
  }

  if (result.jobId) {
    watchExcelJob(result.jobId, fileName, {
      alreadyRunning: result.status === 'Processing',
    });
    return;
  }

  // Every enqueue response carries a job id; if one ever does not, there is nothing to
  // follow, and the Exports drive is still where the file will land.
  message.success('Export queued. It will appear in Exports when ready.');
};
