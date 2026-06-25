import axiosInstance from './AxiosInstance';

export type ActivityEventType = 'Navigation' | 'Logout' | 'Click' | string;

interface TrackEventInput {
  /** Navigation | Logout | Click (free-form). */
  eventType: ActivityEventType;
  /** Client route / target involved (e.g. "/Report/AccountSummaryReport"). */
  path?: string;
  /** Optional extra context (report key, label, etc.) stored as JSON. */
  details?: Record<string, unknown>;
}

/**
 * Fire-and-forget client activity tracker.
 *
 * Posts a single event to the backend, which stamps the user / IP / timestamp
 * server-side and queues it for the audit log. Captures things the backend
 * request-logging middleware can't see on its own — in-app navigation and
 * logout (which make no API call).
 *
 * It never throws and never blocks the UI; it no-ops when the user isn't signed
 * in (the endpoint is auth-gated). Returns a promise so callers that need the
 * event to land before tearing down (e.g. logout clearing the token) can await
 * it; navigation tracking should just call and ignore it.
 */
export const trackEvent = ({
  eventType,
  path,
  details,
}: TrackEventInput): Promise<void> => {
  try {
    // The /client endpoint requires a valid token; skip silently when signed out
    // so a stray post can't trigger the 401 "session expired" interceptor.
    if (!localStorage.getItem('token')) {
      return Promise.resolve();
    }

    return axiosInstance
      .post(
        'ActivityLog/client',
        { EventType: eventType, Path: path, Details: details },
        // Bound the request so awaiting it (logout) can never hang the UI.
        { timeout: 3000 }
      )
      .then(() => undefined)
      .catch(() => undefined);
  } catch {
    return Promise.resolve();
  }
};

export default trackEvent;
