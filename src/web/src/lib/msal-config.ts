import { type AuthenticationResult, type Configuration, LogLevel, PublicClientApplication } from '@azure/msal-browser';

const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined;
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined;
const apiScope = import.meta.env.VITE_API_SCOPE as string | undefined;

const authorityHost = authority ? new URL(authority).hostname : undefined;

export const msalConfig: Configuration = {
  auth: {
    clientId: clientId || 'dev-placeholder',
    authority: authority || undefined,
    knownAuthorities: authorityHost ? [authorityHost] : [],
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: { cacheLocation: 'sessionStorage' },
  system: { loggerOptions: { logLevel: LogLevel.Warning } },
};

export const loginRequest = {
  scopes: apiScope ? [apiScope] : [],
};

export const msalInstance = new PublicClientApplication(msalConfig);

export async function initializeMsal(): Promise<AuthenticationResult | null> {
  await msalInstance.initialize();
  return msalInstance.handleRedirectPromise();
}
