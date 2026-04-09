import { useCallback, useEffect, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import { toNullableNumber } from "../components/admin-config/adminConfigHelpers";
import type { NewResponsibilityDraft } from "../components/admin-config/adminOrganizationTypes";
import { useConfirmationDialog } from "../components/feedback/useConfirmationDialog";
import {
  createAdminResponsibility,
  createAdminDepartment,
  deleteAdminResponsibility,
  deleteAdminDepartment,
  updateAdminDepartmentAssignment,
  updateAdminResponsibilityOwner,
} from "../services/adminApi";
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
  const confirm = useConfirmationDialog();
  const [newDepartmentNameDraft, setNewDepartmentNameDraft] = useState<string>("");
  const [newResponsibilityDraft, setNewResponsibilityDraft] = useState<NewResponsibilityDraft>({
    responsibilityName: "",
    departmentId: "",
  });
  const [departmentDrafts, setDepartmentDrafts] = useState<Record<number, DepartmentDraft>>({});
  const [responsibilityDrafts, setResponsibilityDrafts] = useState<Record<number, ResponsibilityDraft>>({});
  const [isCreatingDepartment, setIsCreatingDepartment] = useState<boolean>(false);
  const [isCreatingResponsibility, setIsCreatingResponsibility] = useState<boolean>(false);
  const [deletingDepartmentId, setDeletingDepartmentId] = useState<number | null>(null);
  const [deletingResponsibilityId, setDeletingResponsibilityId] = useState<number | null>(null);
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

  const createResponsibility = useCallback(async () => {
    const nextResponsibilityName = newResponsibilityDraft.responsibilityName.trim();
    if (!nextResponsibilityName) {
      onNotice(null);
      onError("Bitte einen Zuständigkeitsnamen eingeben.");
      return null;
    }

    setIsCreatingResponsibility(true);
    onNotice(null);
    onError(null);

    try {
      const createdResponsibility = await createAdminResponsibility(
        nextResponsibilityName,
        toNullableNumber(newResponsibilityDraft.departmentId)
      );
      setResponsibilityOwners((current) =>
        current
          .concat(createdResponsibility)
          .sort((left, right) => {
            const leftKey = `${left.responsibilityType}|${left.departmentName ?? ""}|${left.responsibilityName}`;
            const rightKey = `${right.responsibilityType}|${right.departmentName ?? ""}|${right.responsibilityName}`;
            return leftKey.localeCompare(rightKey, "de");
          })
      );
      setNewResponsibilityDraft({ responsibilityName: "", departmentId: "" });
      onNotice(`Zuständigkeit ${createdResponsibility.responsibilityName} wurde angelegt.`);
      return createdResponsibility;
    } catch (err) {
      const message = err instanceof Error ? err.message : "Zuständigkeit konnte nicht angelegt werden.";
      onError(message);
      return null;
    } finally {
      setIsCreatingResponsibility(false);
    }
  }, [newResponsibilityDraft, onError, onNotice, setResponsibilityOwners]);

  const removeDepartment = useCallback(async (department: AdminDepartmentAssignment) => {
    const shouldDelete = await confirm({
      title: "Abteilung löschen?",
      description: `Die Abteilung "${department.departmentName}" wird dauerhaft entfernt. Prüfen Sie vorher, ob abhängige Zuständigkeiten bereits umgestellt wurden.`,
      confirmLabel: "Abteilung löschen",
      tone: "danger",
    });
    if (!shouldDelete) {
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
  }, [confirm, onError, onNotice, reload]);

  const removeResponsibility = useCallback(async (responsibility: AdminResponsibilityOwner) => {
    const shouldDelete = await confirm({
      title: "Zuständigkeit löschen?",
      description: `Die Zuständigkeit "${responsibility.responsibilityName}" wird dauerhaft entfernt. Prüfen Sie vorher, ob Aufgaben oder Workflow-Definitionen noch darauf verweisen.`,
      confirmLabel: "Zuständigkeit löschen",
      tone: "danger",
    });
    if (!shouldDelete) {
      return false;
    }

    setDeletingResponsibilityId(responsibility.responsibilityId);
    onNotice(null);
    onError(null);

    try {
      await deleteAdminResponsibility(responsibility.responsibilityId);
      setResponsibilityOwners((current) =>
        current.filter((item) => item.responsibilityId !== responsibility.responsibilityId)
      );
      setResponsibilityDrafts((current) => {
        const nextDrafts = { ...current };
        delete nextDrafts[responsibility.responsibilityId];
        return nextDrafts;
      });
      onNotice(`Zuständigkeit ${responsibility.responsibilityName} wurde gelöscht.`);
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : "Zuständigkeit konnte nicht gelöscht werden.";
      onError(message);
      return false;
    } finally {
      setDeletingResponsibilityId(null);
    }
  }, [confirm, onError, onNotice, setResponsibilityOwners]);

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
    newResponsibilityDraft,
    departmentDrafts,
    responsibilityDrafts,
    isCreatingDepartment,
    isCreatingResponsibility,
    deletingDepartmentId,
    deletingResponsibilityId,
    savingDepartmentId,
    savingResponsibilityId,
    setNewDepartmentNameDraft,
    setNewResponsibilityDraft,
    setDepartmentDrafts,
    setResponsibilityDrafts,
    createDepartment,
    createResponsibility,
    removeDepartment,
    removeResponsibility,
    saveDepartmentAssignment,
    saveResponsibilityAssignment,
  };
}
