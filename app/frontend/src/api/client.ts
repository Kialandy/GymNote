// All requests go to the relative /api path: the gateway (or Vite proxy in dev) routes it to the backend.
const API_BASE = '/api';
const TOKEN_KEY = 'gymnote.token';

export const tokenStorage = {
  get: (): string | null => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

let onUnauthorized: () => void = () => window.location.assign('/login');

/** Registers what happens when an authenticated request gets a 401 (the app navigates to /login). */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

type RequestOptions = {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  /** Send the stored JWT and treat a 401 as an expired session. Defaults to true. */
  auth?: boolean;
};

type ProblemDetails = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export async function apiFetch<T>(path: string, { method = 'GET', body, auth = true }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const token = tokenStorage.get();
  if (auth && token) headers.Authorization = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'Unable to reach the server. Please try again.');
  }

  if (response.status === 401 && auth) {
    tokenStorage.clear();
    onUnauthorized();
    throw new ApiError(401, 'Your session has expired. Please log in again.');
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorMessage(response));
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `Request failed (${response.status}).`;
  try {
    const problem = (await response.json()) as ProblemDetails;
    const fieldMessages = Object.values(problem.errors ?? {}).flat();
    if (fieldMessages.length > 0) return fieldMessages.join(' ');
    return problem.detail ?? problem.title ?? fallback;
  } catch {
    return fallback;
  }
}
