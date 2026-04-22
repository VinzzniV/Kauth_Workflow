import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "../services/queryKeys";

export function useAdminSharedQueryInvalidation() {
  const queryClient = useQueryClient();

  const invalidateOrganizationLookups = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.roles() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.departments() }),
    ]);
  }, [queryClient]);

  const invalidatePeopleLookups = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["people", "search"] }),
      queryClient.invalidateQueries({ queryKey: ["people", "rotation-eligible"] }),
      queryClient.invalidateQueries({ queryKey: ["workflows", "target-person-sources"] }),
    ]);
  }, [queryClient]);

  return {
    invalidateOrganizationLookups,
    invalidatePeopleLookups,
  };
}
