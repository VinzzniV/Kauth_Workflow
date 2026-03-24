import { useCallback, useEffect, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import { toNullableNumber } from "../components/admin-config/adminConfigHelpers";
import {
  createAdminDepartment,
  deleteAdminDepartment,
  updateAdminDepartmentAssignment,
  updateAdminResponsibilityOwner,
} from "../services/onboardingApi";
import type { AdminDepartmentAssignment, AdminResponsibilityOwner } from "../types/auth";

type DepartmentDraft = {
  departmentLeadUserId: string;
  requirementOwnerUserId: string;
};

type ResponsibilityDraft = {
  appUserId: string;
  departmentId: string;
};

type UseAdminOrganizationManagementOptions = {
  departmentAssignments: AdminDepartmentAssignment[];
  responsibilityOwners: AdminResponsibilityOwner[];
  setDepartmentAssignments: Dispatch<SetStateAction<AdminDepartmentAssignment[]>>;
  setResponsibilityOwners: Dispatch<SetStateAction<AdminResponsibilityOwner[]>>;
  reload: () => Promise<void>;
  onNotice: (message: string | null) => void;
  onError: (message: string | null) => void;
};

export function useAdminOrganizationManagement({
  departmentAssignments,
  responsibilityOwners,
  setDepartmentAssignments,
  setResponsibilityOwners,
  reload,
  onNotice,
  onError,
}: UseAdminOrganizationManagementOptions) {
  const [newDepartmentNameDraft, setNewDepartmentNameDraft] = useState<string>("");
  const [departmentDrafts, setDepartmentDrafts] = useState<Record<number, DepartmentDraft>>({});
  const [responsibilityDrafts, setResponsibilityDrafts] = useState<Record<number, ResponsibilityDraft>>({});
  const [isCreatingDepartment, setIsCreatingDepartment] = useState<boolean>(false);
  const [deletingDepartmentId, setDeletingDepartmentId] = useState<number | null>(null);
  const [savingDepartmentId, setSavingDepartmentId] = useState<number | null>(null);
  const [savingResponsibilityId, setSavingResponsibilityId] = useState<number | null>(null);

  useEffect(() => {
    setDepartmentDrafts(
      Object.fromEntries(
        departmentAssignments.map((item) => [
          item.departmentId,
          {
            departmentLeadUserId: item.departmentLeadUserId ? String(item.departmentLeadUserId) : "",
            requirementOwnerUserId: item.requirementOwnerUserId ? String(item.requirementOwnerUserId) : "",
          },
        ])
      )
    );
  }, [departmentAssignments]);

  useEffect(() => {
    setResponsibilityDrafts(
      Object.fromEntries(
        responsibilityOwners.map((item) => [
          item.responsibilityId,
          {
            appUserId: item.appUserId ? String(item.appUserId) : "",
            departmentId: item.departmentId ? String(item.departmentId) : "",
          },
        ])
      )
    );
  }, [responsibilityOwners]);

  const createDepartment = useCallback(async () => {
    const nextDepartmentName = newDepartmentNameDraft.trim();
    if (!nextDepartmentName) {
      onNotice(null);
      onError("Bitte einen Abteilungsnamen eingeben.");
      return;
    }

    setIsCreatingDepartment(true);
    onNotice(null);
    onError(null);

    try {
      const createdDepartment = await createAdminDepartment(nextDepartmentName);
      setDepartmentAssignments((current) =>
        current.concat(createdDepartment).sort((left, right) => left.departmentName.localeCompare(right.departmentName, "de"))
      );
      setNewDepartmentNameDraft("");
      onNotice(`Abteilung ${createdDepartment.departmentName} wurde angelegt.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abteilung konnte nicht angelegt werden.";
      onError(message);
    } finally {
      setIsCreatingDepartment(false);
    }
  }, [newDepartmentNameDraft, onError, onNotice, setDepartmentAssignments]);

  const removeDepartment = useCallback(async (department: AdminDepartmentAssignment) => {
    if (typeof window !== "undefined" && !window.confirm(`Abteilung "${department.departmentName}" wirklich löschen?`)) {
      return;
    }

    setDeletingDepartmentId(department.departmentId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminDepartment(department.departmentId);
      await reload();
      onNotice(`Abteilung ${department.departmentName} wurde gelöscht.`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Abteilung konnte nicht gelöscht werden.";
      onError(message);
    } finally {
      setDeletingDepartmentId(null);
    }
  }, [onError, onNotice, reload]);

  const saveDepartmentAssignment = useCallback(async (departmentId: number) => {
    const draft = departmentDrafts[departmentId];
    if (!draft) {
      return;
    }

    setSavingDepartmentId(departmentId);
    onNotice(null);
    onError(null);

    try {
      const updatedAssignment = await updateAdminDepartmentAssignment(
        departmentId,
        toNullableNumber(draft.departmentLeadUserId),
        toNullableNumber(draft.requirementOwnerUserId)
      );
      setDepartmentAssignments((current) =>
        current.map((item) => (item.departmentId === updatedAssignment.departmentId ? updatedAssignment : item))
      );
      onNotice(`Zuständigkeiten für ${updatedAssignment.departmentName} wurden gespeichert.`);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Abteilungs-Zuständigkeit konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setSavingDepartmentId(null);
    }
  }, [departmentDrafts, onError, onNotice, setDepartmentAssignments]);

  const saveResponsibilityAssignment = useCallback(async (responsibilityId: number) => {
    const draft = responsibilityDrafts[responsibilityId] ?? { appUserId: "", departmentId: "" };

    setSavingResponsibilityId(responsibilityId);
    onNotice(null);
    onError(null);

    try {
      const updatedAssignment = await updateAdminResponsibilityOwner(
        responsibilityId,
        toNullableNumber(draft.appUserId),
        toNullableNumber(draft.departmentId)
      );
      setResponsibilityOwners((current) =>
        current.map((item) =>
          item.responsibilityId === updatedAssignment.responsibilityId ? updatedAssignment : item
        )
      );
      onNotice(`Zuständigkeit für ${updatedAssignment.responsibilityName} wurde gespeichert.`);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Fachliche Zuständigkeit konnte nicht gespeichert werden.";
      onError(message);
    } finally {
      setSavingResponsibilityId(null);
    }
  }, [onError, onNotice, responsibilityDrafts, setResponsibilityOwners]);

  return {
    newDepartmentNameDraft,
    departmentDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    deletingDepartmentId,
    savingDepartmentId,
    savingResponsibilityId,
    setNewDepartmentNameDraft,
    setDepartmentDrafts,
    setResponsibilityDrafts,
    createDepartment,
    removeDepartment,
    saveDepartmentAssignment,
    saveResponsibilityAssignment,
  };
}
