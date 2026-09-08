import { EventType, PublicClientApplication, type AuthenticationResult } from '@azure/msal-browser';
import { msalConfig } from './msalConfig';

export const msalInstance = new PublicClientApplication(msalConfig);

// Must run before the app renders: MSAL v3+ requires explicit initialize(),
// and the redirect response has to be consumed once on load.
export async function initializeMsal() {
  await msalInstance.initialize();

  const response = await msalInstance.handleRedirectPromise();
  if (response?.account) {
    msalInstance.setActiveAccount(response.account);
  } else {
    const [firstAccount] = msalInstance.getAllAccounts();
    if (firstAccount) {
      msalInstance.setActiveAccount(firstAccount);
    }
  }

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const account = (event.payload as AuthenticationResult).account;
      msalInstance.setActiveAccount(account);
    }
  });
}
