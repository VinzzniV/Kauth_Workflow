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

  // Solange die Session noch nicht aufgeloest ist, trifft der Guard bewusst keine Umleitung.
  if (status !== "authenticated") {
    return null;
  }

  if (canAccessFeature(feature)) {
    return <>{children}</>;
  }

  return <Navigate to={defaultRoute} replace />;
}
