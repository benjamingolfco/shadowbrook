import { describe, it, expect, vi, afterEach } from 'vitest';
import { api, ApiError } from '../api-client';

// Mock msal-config so tests don't require a real MSAL instance
vi.mock('../msal-config', () => ({
  msalInstance: {
    getAllAccounts: vi.fn().mockReturnValue([]),
    acquireTokenSilent: vi.fn(),
    acquireTokenRedirect: vi.fn(),
  },
  loginRequest: { scopes: [] },
}));

function makeResponse(status: number, body: unknown = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('api-client', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('GET returns parsed JSON on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(200, { id: 1 })));

    const result = await api.get('/test');

    expect(result).toEqual({ id: 1 });
  });

  it('POST returns parsed JSON on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(200, { id: 2 })));

    const result = await api.post('/test', { name: 'foo' });

    expect(result).toEqual({ id: 2 });
  });

  it('PUT returns parsed JSON on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(200, { id: 3 })));

    const result = await api.put('/test', { name: 'bar' });

    expect(result).toEqual({ id: 3 });
  });

  it('DELETE returns undefined on success', async () => {
    // DELETE conventionally returns 204 No Content; use a mock object to exercise
    // the 204 branch since the Response constructor rejects a body with status 204.
    const mockResponse = { ok: true, status: 204, json: vi.fn() };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(mockResponse));

    const result = await api.delete('/test');

    expect(result).toBeUndefined();
    expect(mockResponse.json).not.toHaveBeenCalled();
  });

  it('204 returns undefined', async () => {
    // The Response constructor rejects body with status 204 in some environments.
    // Use a plain mock object to exercise the status === 204 branch directly.
    const mockResponse = {
      ok: true,
      status: 204,
      json: vi.fn(),
    };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(mockResponse));

    const result = await api.get<undefined>('/test');

    expect(result).toBeUndefined();
    expect(mockResponse.json).not.toHaveBeenCalled();
  });

  it('throws ApiError with correct status and data on non-OK response', async () => {
    const body = { error: 'Not found' };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(404, body)));

    await expect(api.get('/test')).rejects.toMatchObject({
      name: 'ApiError',
      status: 404,
      data: body,
    });
  });

  it('throws ApiError with correct status on 503', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(503, { error: 'Service unavailable' })));

    await expect(api.get('/test')).rejects.toMatchObject({
      name: 'ApiError',
      status: 503,
    });
  });

  it('throws ApiError with correct status on 500', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(500, { error: 'Internal server error' })));

    await expect(api.get('/test')).rejects.toMatchObject({
      name: 'ApiError',
      status: 500,
    });
  });

  it('ApiError is instanceof Error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(makeResponse(400, { error: 'Bad request' })));

    let thrown: unknown;
    try {
      await api.get('/test');
    } catch (e) {
      thrown = e;
    }

    expect(thrown).toBeInstanceOf(Error);
    expect(thrown).toBeInstanceOf(ApiError);
  });

  it('makes exactly one fetch call per request', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(200, {}));
    vi.stubGlobal('fetch', fetchMock);

    await api.get('/test');

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does not send Authorization header when no token is available', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(200, {}));
    vi.stubGlobal('fetch', fetchMock);

    await api.get('/test');

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect((options.headers as Record<string, string>)['Authorization']).toBeUndefined();
  });
});
