import { useState, useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useToast } from "../components/feedback/useToast";
import {
  createRotationStation,
  deleteRotationStation,
  regenerateRotationGeneratedTasks,
  updateRotationStation,
} from "../services/rotationApi";
import { queryKeys } from "../services/queryKeys";
import type {
  RotationStation,
  RotationStationStatus,
  RotationStationUpsertPayload,
} from "../types/rotation";

export type StationFormState = {
  departmentId: string;
  startDate: string;
  endDate: string;
  orderIndex: string;
  location: string;
  notes: string;
  status: RotationStationStatus;
};

export function createEmptyStationForm(nextOrderIndex = 0): StationFormState {
  return {
    departmentId: "",
    startDate: "",
    endDate: "",
    orderIndex: String(nextOrderIndex),
    location: "",
    notes: "",
    status: "planned",
  };
}

export function buildStationForm(station: RotationStation): StationFormState {
  return {
    departmentId: String(station.departmentId),
    startDate: station.startDate,
    endDate: station.endDate,
    orderIndex: String(station.orderIndex),
    location: station.location ?? "",
    notes: station.notes ?? "",
    status: station.status,
  };
}

export function normalizeStationPayload(form: StationFormState): RotationStationUpsertPayload {
  return {
    departmentId: Number(form.departmentId),
    startDate: form.startDate,
    endDate: form.endDate,
    orderIndex: Number(form.orderIndex),
    location: form.location.trim() || undefined,
    notes: form.notes.trim() || undefined,
    status: form.status,
  };
}

export type UseRotationStationFormArgs = {
  numericPlanId: number;
  personId: number | null;
  orderedStationsCount: number;
};

export function useRotationStationForm({
  numericPlanId,
  personId,
  orderedStationsCount,
}: UseRotationStationFormArgs) {
  const queryClient = useQueryClient();
  const { showError, showSuccess } = useToast();

  const [editingStationId, setEditingStationId] = useState<number | null>(null);
  const [stationForm, setStationForm] = useState<StationFormState>(() => createEmptyStationForm(orderedStationsCount));
  const [isSavingStation, setIsSavingStation] = useState(false);
  const [isRegenerating, setIsRegenerating] = useState(false);
  const [deletingStationId, setDeletingStationId] = useState<number | null>(null);

  useEffect(() => {
    if (editingStationId === null) {
      setStationForm(prev => ({ ...prev, orderIndex: String(orderedStationsCount) }));
    }
  }, [orderedStationsCount, editingStationId]);

  async function reloadPlanData() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.planDetail(numericPlanId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.generatedTasks(numericPlanId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.auditLog(numericPlanId, 100, 0) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.notifications(numericPlanId, 100, 0) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(null) }),
      personId
        ? queryClient.invalidateQueries({ queryKey: queryKeys.rotation.plans(personId) })
        : Promise.resolve(),
    ]);
  }

  function openCreateStationForm() {
    setEditingStationId(null);
    setStationForm(createEmptyStationForm(orderedStationsCount));
  }

  function openEditStationForm(station: RotationStation) {
    setEditingStationId(station.id);
    setStationForm(buildStationForm(station));
  }

  function resetStationForm() {
    setEditingStationId(null);
    setStationForm(createEmptyStationForm(orderedStationsCount));
  }

  async function handleSaveStation() {
    setIsSavingStation(true);
    try {
      const payload = normalizeStationPayload(stationForm);
      if (
        !Number.isFinite(payload.departmentId) ||
        payload.departmentId <= 0 ||
        !payload.startDate ||
        !payload.endDate ||
        !Number.isFinite(payload.orderIndex)
      ) {
        throw new Error("Bitte alle Pflichtfelder der Station korrekt ausfüllen.");
      }

      if (editingStationId) {
        await updateRotationStation(editingStationId, payload);
        showSuccess("Station wurde aktualisiert.");
      } else {
        await createRotationStation(numericPlanId, payload);
        showSuccess("Station wurde angelegt.");
      }

      await reloadPlanData();
      resetStationForm();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Station konnte nicht gespeichert werden.";
      showError(message);
    } finally {
      setIsSavingStation(false);
    }
  }

  async function handleDeleteStation(station: RotationStation) {
    setDeletingStationId(station.id);
    try {
      await deleteRotationStation(station.id);
      showSuccess("Station wurde gelöscht.");
      await reloadPlanData();
      if (editingStationId === station.id) {
        resetStationForm();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Station konnte nicht gelöscht werden.";
      showError(message);
    } finally {
      setDeletingStationId(null);
    }
  }

  async function handleRegenerateTasks() {
    setIsRegenerating(true);
    try {
      const result = await regenerateRotationGeneratedTasks(numericPlanId);
      await reloadPlanData();
      showSuccess(
        `Tasks synchronisiert: ${result.created} neu, ${result.updated} aktualisiert, ${result.cancelled} storniert.`
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : "Task-Synchronisierung ist fehlgeschlagen.";
      showError(message);
    } finally {
      setIsRegenerating(false);
    }
  }

  return {
    editingStationId,
    stationForm,
    setStationForm,
    isSavingStation,
    isRegenerating,
    deletingStationId,
    openCreateStationForm,
    openEditStationForm,
    resetStationForm,
    handleSaveStation,
    handleDeleteStation,
    handleRegenerateTasks,
  };
}
