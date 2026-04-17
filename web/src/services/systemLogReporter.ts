import { identityProvider } from "../auth/IdentityProvider";
import { getApiBase } from "../config/appRuntimeConfig";
import type { ClientLogEventRequest } from "../types/auth";

const LOG_ENDPOINT_PATH = "/client/log-events";
const DEDUPE_WINDOW_MS = 60_000;
const recentEventTimestamps = new Map<string, number>();

function normalizePath(path: string | null | undefined): string {
  return (path ?? "").trim().toLowerCase();
}

function isLogEndpointPath(path: string | null | undefined): boolean {
  const normalized = normalizePath(path);
  return normalized === LOG_ENDPOINT_PATH || normalized.endsWith(LOG_ENDPOINT_PATH);
}

function getCurrentRoute(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function buildDedupeKey(event: ClientLogEventRequest): string {
  return [
    event.source ?? "frontend",
    event.category ?? "ui",
    event.eventKey ?? "user_visible_error",
    event.clientRoute ?? "",
    event.clientFunction ?? "",
    event.httpMethod ?? "",
    event.httpPath ?? "",
    String(event.httpStatus ?? ""),
    event.message ?? "",
    event.userMessage ?? "",
  ].join("|");
}

function shouldReportEvent(event: ClientLogEventRequest): boolean {
  const now = Date.now();
  for (const [key, timestamp] of recentEventTimestamps.entries()) {
    if (now - timestamp > DEDUPE_WINDOW_MS) {
      recentEventTimestamps.delete(key);
    }
  }

  const dedupeKey = buildDedupeKey(event);
  const lastSentAt = recentEventTimestamps.get(dedupeKey);
  if (lastSentAt && now - lastSentAt < DEDUPE_WINDOW_MS) {
    return false;
  }

  recentEventTimestamps.set(dedupeKey, now);
  return true;
}

export async function reportClientLogEvent(event: ClientLogEventRequest): Promise<void> {
  if (typeof window === "undefined") {
    return;
  }

  const normalizedEvent: ClientLogEventRequest = {
    ...event,
    clientRoute: event.clientRoute ?? getCurrentRoute(),
  };

  if (isLogEndpointPath(normalizedEvent.httpPath) || !shouldReportEvent(normalizedEvent)) {
    return;
  }

  try {
    const token = await Promise.resolve(identityProvider.getStoredToken()).catch(() => null);
    await fetch(`${getApiBase()}${LOG_ENDPOINT_PATH}`, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(normalizedEvent),
    });
  } catch {
    // Reporting must never affect the original user flow.
  }
}

export function reportUserVisibleError(args: {
  message: string;
  userMessage?: string | null;
  severity?: "info" | "warning" | "error";
  source?: string;
  category?: string;
  eventKey?: string;
  clientFunction?: string | null;
  httpMethod?: string | null;
  httpPath?: string | null;
  httpStatus?: number | null;
  traceIdentifier?: string | null;
  workflowUid?: string | null;
  rotationPlanId?: number | null;
  taskRef?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  details?: unknown | null;
}): void {
  void reportClientLogEvent({
    severity: args.severity ?? "error",
    source: args.source ?? "frontend",
    category: args.category ?? "ui",
    eventKey: args.eventKey ?? "user_visible_error",
    message: args.message,
    userMessage: args.userMessage ?? args.message,
    clientFunction: args.clientFunction ?? null,
    httpMethod: args.httpMethod ?? null,
    httpPath: args.httpPath ?? null,
    httpStatus: args.httpStatus ?? null,
    traceIdentifier: args.traceIdentifier ?? null,
    workflowUid: args.workflowUid ?? null,
    rotationPlanId: args.rotationPlanId ?? null,
    taskRef: args.taskRef ?? null,
    entityType: args.entityType ?? null,
    entityId: args.entityId ?? null,
    details: args.details ?? null,
  });
}
