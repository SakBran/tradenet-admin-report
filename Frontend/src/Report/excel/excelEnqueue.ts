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

/**
 * Enqueues the export and reports its outcome to the user.
 *
 * @param route            the report's `excelRoute` (e.g. `MPUReport/Excel`)
 * @param request          the grid request body (filters + paging)
 * @param spec             the presentation spec, posted as `excel`
 * @param fallbackFileName file name to save under when the job does not name one
 * @throws ExcelSpecRejectedError on HTTP 400 (stale bundle); rethrows anything else
 */
export const enqueueExcelExport = async (
  route: string,
  request: Record<string, unknown>,
  spec: ExcelPresentationSpec,
  fallbackFileName: string
): Promise<void> => {
  let result: ExcelEnqueueResult;

  try {
    const response = await axiosInstance.post<ExcelEnqueueResult>(route, {
      ...request,
      excel: spec,
    });
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
    const fileResponse = await axiosInstance.get(result.downloadUrl, {
      responseType: 'blob',
    });
    const blob = new Blob([fileResponse.data], {
      type: String(fileResponse.headers['content-type'] ?? excelContentType),
    });
    downloadBlob(blob, result.fileName ?? fallbackFileName);
    message.success('Your Excel export is ready and downloading.');
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
