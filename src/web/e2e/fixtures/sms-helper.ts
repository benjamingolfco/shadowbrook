import { API_BASE_URL } from '../playwright.config';

interface DevSmsMessage {
  id: string;
  from: string;
  to: string;
  body: string;
  direction: string;
  timestamp: string;
}

/**
 * Poll the dev SMS API for a message matching the given predicate.
 * Returns the first matching message body, or throws after timeout.
 */
export async function waitForSms(
  phoneNumber: string,
  predicate: (body: string) => boolean,
  { timeoutMs = 15_000, intervalMs = 500 } = {},
): Promise<string> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const response = await fetch(`${API_BASE_URL}/dev/sms/conversations/${phoneNumber}`);

    if (response.ok) {
      const messages: DevSmsMessage[] = await response.json();
      // Use findLast to get the most recent match — avoids stale SMS from prior runs
      const match = messages.findLast((m) => predicate(m.body));
      if (match) {
        return match.body;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  throw new Error(`SMS matching predicate not received within ${timeoutMs}ms for phone ${phoneNumber}`);
}

/**
 * Extract the offer URL path from an SMS body.
 * SMS format: "... Claim your spot: {baseUrl}/book/walkup/{token}"
 * Returns the full path: "/book/walkup/{token}"
 */
export function extractOfferUrl(smsBody: string): string {
  const match = smsBody.match(/\/book\/walkup\/[\w-]+/);
  if (!match) {
    throw new Error(`Could not extract offer URL from SMS: "${smsBody}"`);
  }
  return match[0];
}
