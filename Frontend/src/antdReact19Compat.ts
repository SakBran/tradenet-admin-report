/**
 * Lets antd v5's static APIs -- `message.*`, `notification.*`, `Modal.confirm` -- render
 * under React 19.
 *
 * antd renders them through `createRoot` / `ReactDOM.render` looked up on the `react-dom`
 * main entry, and React 19 removed both from it. Without this hook every one of those
 * calls was a silent no-op (rc-util's `render()` falls through to a legacy path whose
 * `ReactDOM.render` is undefined): no error, and no warning in a production build. The app
 * has run React 19 since its first commit, so "Export queued", "Excel export failed",
 * "Login fail", the session-expired notice and every other static toast had never been
 * shown to a user.
 *
 * This is antd's documented fix (https://u.ant.design/v5-for-19) -- what the
 * `@ant-design/v5-patch-for-react-19` package does, without the extra dependency. It must
 * run before anything calls a static method, hence the first import in main.tsx.
 * Delete this file when moving to antd v6, which supports React 19 itself.
 */
import { message, notification, unstableSetRender } from 'antd';
import { createRoot, type Root } from 'react-dom/client';

type RootContainer = (Element | DocumentFragment) & { _reactRoot?: Root };

unstableSetRender((node, container) => {
  const host = container as RootContainer;
  const root = host._reactRoot ?? createRoot(host);
  host._reactRoot = root;
  root.render(node);

  return async () => {
    // Next tick, as in antd's snippet: React will not unmount a root synchronously
    // while it is still rendering, and antd can close a holder mid-render.
    await new Promise((resolve) => setTimeout(resolve, 0));
    root.unmount();
    delete host._reactRoot;
  };
});

// These toasts are on screen for the first time. Cap how many can stack at once so a
// burst -- several requests failing together -- cannot bury the page.
message.config({ maxCount: 3 });

// Notifications (the Excel export notices) open top-right, where the 64px sticky header
// keeps the account menu. Start them below it so a sticky notice never covers Logout.
notification.config({ top: 80 });
