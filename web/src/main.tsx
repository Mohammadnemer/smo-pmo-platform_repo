import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { initializeMsal } from './auth/msalInstance';
import './shared/i18n/i18n';
import './shared/theme/theme';
import './index.css';

// MSAL must finish initializing (and consume any redirect response) before
// the app renders — see auth/msalInstance.ts.
async function bootstrap() {
  await initializeMsal();

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

void bootstrap();
