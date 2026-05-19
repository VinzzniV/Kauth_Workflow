export type SystemConfigSecretStatus = "set" | "unset";
export type SystemConfigVaultStatus = "present" | "missing";

export interface SystemConfigEntra {
  tenantId: string | null;
  clientId: string | null;
  audience: string | null;
  clientSecretStatus: SystemConfigSecretStatus;
  graphClientSecretStatus: SystemConfigSecretStatus;
}

export interface SystemConfigDirectory {
  syncScheduled: boolean;
  syncIntervalMinutes: number;
  groupPrefix: string | null;
  explicitGroupIds: string | null;
  autoProvisionDefaultRoleKey: string | null;
}

export interface SystemConfigEmail {
  enabled: boolean;
  provider: string;
  senderEmail: string | null;
  frontendBaseUrl: string;
  saveToSentItems: boolean;
}

export interface SystemConfigVault {
  keyStatus: SystemConfigVaultStatus;
}

export interface SystemConfigRetry {
  maxAttempts: number;
  firstRetryDelaySeconds: number;
  subsequentRetryDelaySeconds: number;
}

export interface SystemConfigWorkerLease {
  staleClaimTimeoutMinutes: number;
}

export interface SystemConfigHostHealth {
  enabled: boolean;
  procfsPath: string | null;
  rootPath: string | null;
  storagePaths: string | null;
}

export interface SystemConfigSnapshot {
  environment: string;
  isProduction: boolean;
  authMode: string;
  swaggerEnabled: boolean;
  publicBaseUrl: string | null;
  entra: SystemConfigEntra;
  directory: SystemConfigDirectory;
  email: SystemConfigEmail;
  vault: SystemConfigVault;
  retry: SystemConfigRetry;
  workerLease: SystemConfigWorkerLease;
  hostHealth: SystemConfigHostHealth;
}
