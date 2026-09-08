import { MsalProvider } from '@azure/msal-react';
import { QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { msalInstance } from '../auth/msalInstance';
import { queryClient } from '../shared/api/queryClient';

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <MsalProvider instance={msalInstance}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </MsalProvider>
  );
}
