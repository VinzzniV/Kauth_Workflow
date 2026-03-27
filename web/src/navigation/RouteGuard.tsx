// Schuetzt einzelne Routen ueber dieselbe Feature-Logik wie Navigation und Dashboard.
import { Navigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useCurrentUser } from "../auth/useCurrentUser";
import type { AppFeature } from "../auth/roleModel";

type Props = {
  feature: AppFeature;
  children: ReactNode;
};

export default function RouteGuard({ feature, children }: Props) {
  const { status, canAccessFeature, defaultRoute } = useCurrentUser();

  // Waehrend der Initialisierung uebernimmt App.tsx das globale Laden-Feedback.
  if (status === "loading") {
    return null;
  }

  // Defensiv: sollte App.tsx schon abfangen, aber kein Blank-Screen bei unauthenticated.
  if (status !== "authenticated") {
    return <Navigate to="/" replace />;
  }

  if (canAccessFeature(feature)) {
    return <>{children}</>;
  }

  return <Navigate to={defaultRoute} replace />;
}
