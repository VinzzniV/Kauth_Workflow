import type { Me, SimulationLoginResponse, SimulationLoginUserOption } from "../types/auth";
import { getDevSimAuthToken, requestJson, setDevSimAuthToken } from "./api/client";
import type { BackendMeDto, BackendSimulationLoginUserOptionDto } from "./api/backendDtos";

type BackendSimulationLoginResponseDto = {
  token: string;
  expiresAtUtc: string;
  user: BackendMeDto;
};

export { getDevSimAuthToken, setDevSimAuthToken };

export async function getSimulationLoginUsers(): Promise<SimulationLoginUserOption[]> {
  return requestJson<BackendSimulationLoginUserOptionDto[]>("/auth/sim-users");
}

export async function simulationLogin(userId: number): Promise<SimulationLoginResponse> {
  const data = await requestJson<BackendSimulationLoginResponseDto>("/auth/sim-login", {
    method: "POST",
    body: { userId },
  });

  return {
    token: data.token,
    expiresAtUtc: data.expiresAtUtc,
    user: data.user,
  };
}

export async function simulationLogout(): Promise<void> {
  await requestJson<void>("/auth/sim-logout", { method: "POST" });
}

export async function getMe(): Promise<Me> {
  return requestJson<BackendMeDto>("/me");
}
