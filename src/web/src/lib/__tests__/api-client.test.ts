import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { api, setActiveTenantId } from '../api-client';

function makeResponse(status: number, body: unknown = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('api-client retry on 503', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    setActiveTenantId(null);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('succeeds immediately on a 200 without retrying', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(200, { id: 1 }));
    vi.stubGlobal('fetch', fetchMock);

    const promise = api.get('/test');
    await vi.runAllTimersAsync();
    const result = await promise;

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(result).toEqual({ id: 1 });
  });

  it('retries on 503 and succeeds when the server recovers', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(makeResponse(503))
      .mockResolvedValueOnce(makeResponse(503))
      .mockResolvedValueOnce(makeResponse(200, { id: 2 }));
    vi.stubGlobal('fetch', fetchMock);

    const promise = api.get('/test');
    await vi.runAllTimersAsync();
    const result = await promise;

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(result).toEqual({ id: 2 });
  });

  it('throws after exhausting all retries when every response is 503', async () => {
    // 1 initial attempt + 3 retries = 4 total calls
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(503));
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      Promise.all([api.get('/test'), vi.runAllTimersAsync()]),
    ).rejects.toMatchObject({ status: 503 });
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it('uses exponential backoff delays between retries', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(503));
    vi.stubGlobal('fetch', fetchMock);

    const promise = api.get('/test').catch(() => {
      /* handled below */
    });

    // First attempt fires immediately — no delay before attempt 0
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    // Retry 1: delay = 2000ms (base * 2^0)
    await vi.advanceTimersByTimeAsync(1999);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(fetchMock).toHaveBeenCalledTimes(2);

    // Retry 2: delay = 4000ms (base * 2^1)
    await vi.advanceTimersByTimeAsync(3999);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(1);
    expect(fetchMock).toHaveBeenCalledTimes(3);

    // Retry 3: delay = 8000ms (base * 2^2)
    await vi.advanceTimersByTimeAsync(7999);
    expect(fetchMock).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(1);
    expect(fetchMock).toHaveBeenCalledTimes(4);

    // No more retries
    await vi.runAllTimersAsync();
    expect(fetchMock).toHaveBeenCalledTimes(4);

    await promise;
  });

  it('does not retry on non-503 errors', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(404, { error: 'Not found' }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(api.get('/test')).rejects.toMatchObject({ status: 404 });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does not retry on 500 errors', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeResponse(500, { error: 'Internal server error' }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(api.get('/test')).rejects.toMatchObject({ status: 500 });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
