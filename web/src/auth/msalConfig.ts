import type { Configuration } from '@azure/msal-browser';

// Placeholder values until the Entra External ID tenant app registration exists
// (see docs/sessions/B2.md follow-ups). Override via web/.env.local.
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID || '00000000-0000-0000-0000-000000000000';
const authority =
  import.meta.env.VITE_ENTRA_AUTHORITY || 'https://login.microsoftonline.com/tenant-id/v2.0';
const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin;
const apiScope =
  import.meta.env.VITE_API_SCOPE ||
  'api://1fa395ad-1f77-409c-a4d9-9b9fd699bb1f/smo-pmo-platform/access_as_user';

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
};

export const apiRequest = {
  scopes: [apiScope],
};

export const loginRequest = {
  scopes: ['openid', 'profile', apiScope],
};
