import { vi } from 'vitest';

export function jsonResponse(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
  });
}

type Handler = (url: string, init: RequestInit) => Response | Promise<Response>;

/** Replaces global fetch with a mock; the handler receives the URL and request init. */
export function mockFetch(handler: Handler) {
  const fetchMock = vi.fn<(url: string, init: RequestInit) => Promise<Response>>((url, init = {}) =>
    Promise.resolve(handler(String(url), init)),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}
