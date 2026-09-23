import { describe, expect, it, vi } from 'vitest';

type Render = (node: unknown, container: object) => () => Promise<void>;

const unstableSetRender = vi.fn();
const messageConfig = vi.fn();
const notificationConfig = vi.fn();

vi.mock('antd', () => ({
  unstableSetRender: (...args: unknown[]) => unstableSetRender(...args),
  message: { config: (...args: unknown[]) => messageConfig(...args) },
  notification: { config: (...args: unknown[]) => notificationConfig(...args) },
}));

const render = vi.fn();
const unmount = vi.fn();
const createRoot = vi.fn(() => ({ render, unmount }));

vi.mock('react-dom/client', () => ({
  createRoot: (...args: unknown[]) => createRoot(...(args as [])),
}));

await import('./antdReact19Compat');

/**
 * Without this module every static antd call -- message.*, notification.*, Modal.confirm
 * -- rendered nothing under React 19 (react-dom's main entry has no createRoot/render any
 * more). These pin the renderer it installs.
 */
describe('antdReact19Compat', () => {
  it('installs a React 19 renderer for antd static methods on import', () => {
    expect(unstableSetRender).toHaveBeenCalledTimes(1);
    expect(unstableSetRender.mock.calls[0][0]).toBeTypeOf('function');
  });

  it('renders into one root per container and unmounts it', async () => {
    const renderInto = unstableSetRender.mock.calls[0][0] as Render;
    const container = {};

    const unmountFirst = renderInto({ n: 1 }, container);
    renderInto({ n: 2 }, container);

    // antd re-renders its holder into the same container: one root, reused.
    expect(createRoot).toHaveBeenCalledTimes(1);
    expect(render).toHaveBeenNthCalledWith(1, { n: 1 });
    expect(render).toHaveBeenNthCalledWith(2, { n: 2 });

    await unmountFirst();
    expect(unmount).toHaveBeenCalledTimes(1);

    // Once unmounted the container gets a fresh root, never the dead one.
    renderInto({ n: 3 }, container);
    expect(createRoot).toHaveBeenCalledTimes(2);
  });

  it('caps stacked toasts and keeps notifications below the sticky header', () => {
    expect(messageConfig).toHaveBeenCalledWith({ maxCount: 3 });
    expect(notificationConfig).toHaveBeenCalledWith({ top: 80 });
  });
});
