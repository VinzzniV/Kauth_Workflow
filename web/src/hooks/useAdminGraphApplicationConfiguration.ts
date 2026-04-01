import { useState } from "react";
import type { AdminGraphApplicationConfiguration } from "../types/auth";

export function useAdminGraphApplicationConfiguration() {
  const [graphApplicationConfiguration, setGraphApplicationConfiguration] =
    useState<AdminGraphApplicationConfiguration | null>(null);

  return {
    graphApplicationConfiguration,
    setGraphApplicationConfiguration,
  };
}
