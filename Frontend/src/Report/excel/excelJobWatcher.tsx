/**
 * Follows queued Excel export jobs in the background and hands over each file when its
 * job finishes, so the Excel button never has to wait for the worker.
 *
 * Module-level on purpose: a job outlives the page that started it (the user moves on to
 * another report while the worker is still writing the file), so nothing here belongs to
 * a component. Both entry points are fire-and-forget and never throw -- the job is already
 * queued by the time it gets here and the Exports drive always has the file, so a hiccup
 * while following it must never be reported as a failed export.
 */
import axios from 'axios';
import type { ReactElement } from 'react';
import { Button, message, notification, Space } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';

import axiosInstance from '../../services/AxiosInstance';

const excelContentType =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** One row of `GET ExcelExport/{id}`, the subset the follower reads. */
interface ExcelJobStatus {
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  fileName?: string | null;
  downloadUrl?: string | null;
  errorMessage?: string | null;
}

/**
 * What the progress notice says. `Requested` is a job this click did not create: the
 * enqueue answered `Processing` because an identical export was already in flight, and
 * whether that one is still queued or already generating is not known until the first
 * poll. `Retrying` is a failed attempt the worker has put back in the queue.
 */
type ProgressPhase =
  | 'Requested'
  | 'Queued'
  | 'Retrying'
  | 'Processing'
  | 'Downloading';

interface WatchedJob {
  fileName: string;
  phase: ProgressPhase;
  /** The worker's error from the attempt being retried. */
  retryReason?: string;
  /** The user closed the progress notice: keep following, but stop re-opening it. */
  dismissed: boolean;
}

const watched = new Map<string, WatchedJob>();

/**
 * How long to follow one job: the worker's lease (`ExcelExport:LeaseMinutes`, 30). By
 * then the job has either finished or is being retried, and the Exports drive shows which.
 */
const FOLLOW_BUDGET_MS = 30 * 60_000;

/**
 * Brisk while a typical export is likely to finish, then backing off for the long ones.
 * Every poll is also a row in the activity log (ActivityLoggingMiddleware records each
 * /api request, GETs included, for a year), so this stays well under the 60 reads a
 * minute of the old follow loop.
 */
const pollDelayMs = (elapsedMs: number) =>
  elapsedMs < 10_000
    ? 1_000
    : elapsedMs < 60_000
      ? 2_000
      : elapsedMs < 300_000
        ? 5_000
        : 15_000;

/** How long a finished file stays in memory for its notice's Download button. */
const KEEP_FILE_MS = 10 * 60_000;

let openExports: (() => void) | undefined;

/**
 * Lets the notices offer an "Open Exports" button. The app registers its router here;
 * without a registration the notices simply do not show the button.
 */
export const setOpenExports = (open: () => void): void => {
  openExports = open;
};

const noticeKey = (jobId: string) => `excel-job-${jobId}`;

const delay = (ms: number) =>
  new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });

const httpStatus = (error: unknown) =>
  axios.isAxiosError(error) ? error.response?.status : undefined;

/** The export's row or its file is gone (deleted from Exports, or past its retention). */
const isGone = (error: unknown) => {
  const status = httpStatus(error);
  return status === 404 || status === 410;
};

const currentToken = () => {
  try {
    return typeof localStorage === 'undefined'
      ? null
      : localStorage.getItem('token');
  } catch {
    return null;
  }
};

const isTabHidden = () =>
  typeof document !== 'undefined' && document.visibilityState === 'hidden';

const fetchFile = async (url: string) => {
  const fileResponse = await axiosInstance.get(url, { responseType: 'blob' });
  return new Blob([fileResponse.data], {
    type: String(fileResponse.headers['content-type'] ?? excelContentType),
  });
};

const saveFile = (blob: Blob, fileName: string) => {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

const downloadAgain = (url: string, fileName: string) => {
  fetchFile(url)
    .then((blob) => saveFile(blob, fileName))
    .catch((error: unknown) => {
      message.error(
        isGone(error)
          ? 'The export file is no longer available. Please export it again.'
          : 'Could not download the export. Open Exports to download it.'
      );
    });
};

const downloadButton = (onClick: () => void) => (
  <Button key="download" type="primary" size="small" onClick={onClick}>
    Download
  </Button>
);

const openExportsButton = () =>
  openExports ? (
    <Button key="exports" size="small" onClick={() => openExports?.()}>
      Open Exports
    </Button>
  ) : null;

const actionsOf = (...buttons: (ReactElement | null)[]) => {
  const shown = buttons.filter((button): button is ReactElement => !!button);
  return shown.length ? <Space size="small">{shown}</Space> : undefined;
};

const keepWorking =
  'You can keep working: it will download automatically when ready, and it will also appear in Exports.';

const progressDescription = (job: WatchedJob) => {
  switch (job.phase) {
    case 'Requested':
      return `The same export was already requested and is in progress. ${keepWorking}`;
    case 'Queued':
      return `Waiting for the export worker. ${keepWorking}`;
    case 'Retrying':
      return `Retrying after an error (${job.retryReason}). ${keepWorking}`;
    case 'Processing':
      return `Generating the file. ${keepWorking}`;
    case 'Downloading':
      return 'Downloading the file.';
  }
};

const showProgress = (jobId: string, job: WatchedJob) => {
  notification.open({
    key: noticeKey(jobId),
    message: `Preparing ${job.fileName}`,
    description: progressDescription(job),
    icon: <LoadingOutlined />,
    duration: 0,
    onClose: () => {
      job.dismissed = true;
    },
  });
};

const updateProgress = (
  jobId: string,
  job: WatchedJob,
  phase: ProgressPhase
) => {
  if (job.phase === phase) {
    return;
  }

  job.phase = phase;
  if (!job.dismissed) {
    showProgress(jobId, job);
  }
};

/** Downloads a finished export and replaces its progress notice with the outcome. */
const deliver = async (jobId: string, url: string, fileName: string) => {
  const key = noticeKey(jobId);

  let file: Blob;
  try {
    file = await fetchFile(url);
  } catch (error) {
    if (isGone(error)) {
      notification.warning({
        key,
        message: 'Excel export no longer available',
        description: `${fileName} was removed from Exports. Please export it again.`,
        duration: 0,
      });
      return;
    }

    notification.info({
      key,
      message: `${fileName} is ready`,
      description: 'The download did not start. Use Download, or open Exports.',
      actions: actionsOf(
        downloadButton(() => downloadAgain(url, fileName)),
        openExportsButton()
      ),
      duration: 0,
    });
    return;
  }

  saveFile(file, fileName);

  // A download fired long after the click carries no user gesture, and a browser may
  // block it as an automatic download. The notice's Download button saves the kept file
  // with no await in between, so that click counts as the gesture and always goes through.
  let kept: Blob | null = file;
  const release = () => {
    kept = null;
  };
  setTimeout(release, KEEP_FILE_MS);

  notification.success({
    key,
    message: `${fileName} is ready`,
    description: 'Your Excel export is downloading.',
    actions: actionsOf(
      downloadButton(() =>
        kept ? saveFile(kept, fileName) : downloadAgain(url, fileName)
      )
    ),
    // Finished while the user was on another tab: keep the notice until they are back.
    duration: isTabHidden() ? 0 : 15,
    onClose: release,
  });
};

const follow = async (jobId: string, job: WatchedJob) => {
  const key = noticeKey(jobId);
  const startedAt = Date.now();
  const token = currentToken();

  while (Date.now() - startedAt < FOLLOW_BUDGET_MS) {
    await delay(pollDelayMs(Date.now() - startedAt));

    // Signed out, or someone else signed in on this browser: the file is no longer this
    // session's to hand over. It is still in Exports.
    if (currentToken() !== token) {
      watched.delete(jobId);
      notification.destroy(key);
      return;
    }

    let current: ExcelJobStatus;
    try {
      const { data } = await axiosInstance.get<ExcelJobStatus>(
        `ExcelExport/${jobId}`
      );
      current = data;
    } catch (error) {
      const status = httpStatus(error);

      if (status === 404) {
        watched.delete(jobId);
        notification.warning({
          key,
          message: 'Excel export removed',
          description: `${job.fileName} was deleted from Exports before it finished.`,
          duration: 0,
        });
        return;
      }

      if (status === 401 || status === 403) {
        // The session has expired and the layout's interceptor is signing the user out;
        // polling on would only repeat the 401.
        watched.delete(jobId);
        notification.info({
          key,
          message: `${job.fileName} is still being prepared`,
          description:
            'Your session ended. Sign in again to download it from Exports.',
          duration: 8,
        });
        return;
      }

      // Keep polling: one failed status read says nothing about the job.
      continue;
    }

    if (current.status === 'Completed') {
      watched.delete(jobId);
      updateProgress(jobId, job, 'Downloading');
      await deliver(
        jobId,
        current.downloadUrl ?? `ExcelExport/${jobId}/download`,
        current.fileName ?? job.fileName
      );
      return;
    }

    if (current.status === 'Failed') {
      watched.delete(jobId);
      notification.error({
        key,
        message: 'Excel export failed',
        description: current.errorMessage
          ? `${job.fileName}: ${current.errorMessage}`
          : `${job.fileName} could not be generated. Please try again.`,
        duration: 0,
      });
      return;
    }

    // A failed attempt that will be retried goes back to Queued with its error kept;
    // only `Failed` is final.
    job.retryReason = current.errorMessage ?? undefined;
    updateProgress(
      jobId,
      job,
      current.status === 'Queued' && current.errorMessage
        ? 'Retrying'
        : current.status
    );
  }

  watched.delete(jobId);
  notification.info({
    key,
    message: `${job.fileName} is still being generated`,
    description: 'It will appear in Exports when ready.',
    actions: actionsOf(openExportsButton()),
    duration: 0,
  });
};

/**
 * Follows a queued job to completion in the background, then downloads the file.
 *
 * Idempotent per job: an export whose identical twin is already in flight comes back from
 * the enqueue as `Processing` with the SAME job id, so a second click re-shows that job's
 * notice instead of starting another poll loop (and another download).
 */
export const watchExcelJob = (
  jobId: string,
  fileName: string,
  { alreadyRunning = false }: { alreadyRunning?: boolean } = {}
): void => {
  const existing = watched.get(jobId);
  if (existing) {
    existing.dismissed = false;
    showProgress(jobId, existing);
    return;
  }

  const job: WatchedJob = {
    fileName,
    phase: alreadyRunning ? 'Requested' : 'Queued',
    dismissed: false,
  };
  watched.set(jobId, job);
  showProgress(jobId, job);
  follow(jobId, job).catch(() => {
    // Unreachable by design (every step above handles its own failure); the guard only
    // keeps a surprise from surfacing as an unhandled rejection. Exports has the file.
    watched.delete(jobId);
  });
};

/** Downloads an export the queue reused (a closed period's finished file) in the background. */
export const deliverReadyExport = (
  jobId: string,
  downloadUrl: string,
  fileName: string
): void => {
  showProgress(jobId, { fileName, phase: 'Downloading', dismissed: false });
  deliver(jobId, downloadUrl, fileName).catch(() => {
    // Same guard as watchExcelJob: deliver reports its own failures.
  });
};
