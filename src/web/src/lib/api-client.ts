import { msalInstance, loginRequest } from './msal-config';

const BASE_URL = import.meta.env.VITE_API_URL ?? '';
const useDevAuth = import.meta.env.VITE_USE_DEV_AUTH === 'true';
const devIdentityId = import.meta.env.VITE_DEV_IDENTITY_ID ?? '';

async function getAuthToken(): Promise<string | null> {
  if (useDevAuth) return devIdentityId || null;
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) return null;
  try {
    const response = await msalInstance.acquireTokenSilent({
      ...loginRequest,
      account: accounts[0],
    });
    return response.accessToken;
  } catch {
    await msalInstance.acquireTokenRedirect(loginRequest);
    return null;
  }
}

export class ApiError extends Error {
  status: number;
  data?: unknown;

  constructor(message: string, status: number, data?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.data = data;
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options?.headers as Record<string, string>),
  };

  const token = await getAuthToken();
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    const errorMessage = error.error || error.title || response.statusText || `Request failed (${response.status})`;
    throw new ApiError(errorMessage, response.status, error);
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
