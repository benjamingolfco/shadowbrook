const BASE_URL = import.meta.env.VITE_API_URL ?? '';

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

  const response = await fetch(`${BASE_URL}${path}`, {
    headers,
    ...options,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    const errorMessage = error.error ?? error.title ?? response.statusText ?? 'Request failed';

    // Include status code for better error handling downstream
    const errorWithStatus = new Error(errorMessage) as Error & { status?: number };
    errorWithStatus.status = response.status;
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
