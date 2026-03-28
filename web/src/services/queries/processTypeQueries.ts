import { useQuery } from "@tanstack/react-query";
import { getProcessTypes } from "../lookupApi";
import { queryKeys } from "../queryKeys";

export function useProcessTypes() {
  return useQuery({
    queryKey: queryKeys.processTypes(),
    queryFn: getProcessTypes,
    staleTime: 5 * 60 * 1000,
  });
}
