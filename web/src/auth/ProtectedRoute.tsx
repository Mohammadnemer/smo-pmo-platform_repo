import { InteractionType } from '@azure/msal-browser';
import { MsalAuthenticationTemplate } from '@azure/msal-react';
import type { PropsWithChildren } from 'react';
import { loginRequest } from './msalConfig';

// Dev-only escape hatch: skips the Entra External ID redirect so the app is reachable
// before a real tenant app registration exists (msalConfig falls back to a placeholder
// client ID otherwise). Set VITE_SKIP_AUTH=true in web/.env.local; never set it anywhere
// deployed — it bypasses F1's "unauthenticated users get redirected to sign-in" entirely.
const skipAuth = import.meta.env.VITE_SKIP_AUTH === 'true';

// Wrapping the router in this sends unauthenticated users straight into the
// Entra External ID redirect flow — this is what makes F1's "Done when" true.
export function ProtectedRoute({ children }: PropsWithChildren) {
  if (skipAuth) {
    return <>{children}</>;
  }

  return (
    <MsalAuthenticationTemplate
      interactionType={InteractionType.Redirect}
      authenticationRequest={loginRequest}
      loadingComponent={() => <div className="auth-status">Redirecting to sign-in…</div>}
      errorComponent={({ error }) => (
        <div className="auth-status auth-status--error">
          Sign-in failed{error ? `: ${error.errorMessage}` : '.'}
        </div>
      )}
    >
      {children}
    </MsalAuthenticationTemplate>
  );
}
