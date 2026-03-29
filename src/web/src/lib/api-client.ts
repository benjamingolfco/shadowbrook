const BASE_URL = import.meta.env.VITE_API_URL ?? '';

const RETRY_COUNT = 3;
const RETRY_BASE_DELAY_MS = 2000;

let activeTenantId: string | null = null;

export function setActiveTenantId(id: string | null): void {
  activeTenantId = id;
}

export function getActiveTenantId(): string | null {
  return activeTenantId;
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...options?.headers,
  };

  if (activeTenantId) {
    (headers as Record<string, string>)['X-Tenant-Id'] = activeTenantId;
  }

  let lastResponse: Response | undefined;

  for (let attempt = 0; attempt <= RETRY_COUNT; attempt++) {
    if (attempt > 0) {
      const delay = RETRY_BASE_DELAY_MS * Math.pow(2, attempt - 1);
      await new Promise((resolve) => setTimeout(resolve, delay));
    }

    const response = await fetch(`${BASE_URL}${path}`, {
      headers,
      ...options,
    });

    if (response.status !== 503) {
      lastResponse = response;
      break;
    }

    lastResponse = response;
  }

  const response = lastResponse!;

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    const errorMessage = error.error || error.title || response.statusText || `Request failed (${response.status})`;

    // Include status code and full body for better error handling downstream
    const errorWithStatus = new Error(errorMessage) as Error & { status?: number; data?: unknown };
    errorWithStatus.status = response.status;
    errorWithStatus.data = error;
    throw errorWithStatus;
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: (path: string) => request<void>(path, { method: 'DELETE' }),
};
