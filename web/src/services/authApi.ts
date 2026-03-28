import type { DemoLoginResponse, DemoLoginUserOption, Me } from "../types/auth";
import { getDemoAuthToken, requestJson, setDemoAuthToken } from "./api/client";
import type { BackendDemoLoginUserOptionDto, BackendMeDto } from "./api/backendDtos";

type BackendDemoLoginResponseDto = {
  token: string;
  expiresAtUtc: string;
  user: BackendMeDto;
};

export { getDemoAuthToken, setDemoAuthToken };

export async function getDemoLoginUsers(): Promise<DemoLoginUserOption[]> {
  return requestJson<BackendDemoLoginUserOptionDto[]>("/auth/demo-users");
}

export async function demoLogin(username: string): Promise<DemoLoginResponse> {
  const data = await requestJson<BackendDemoLoginResponseDto>("/auth/demo-login", {
    method: "POST",
    body: { username },
  });

  return {
    token: data.token,
    expiresAtUtc: data.expiresAtUtc,
    user: data.user,
  };
}

export async function demoLogout(): Promise<void> {
  await requestJson<unknown>("/auth/demo-logout", { method: "POST" });
}

export async function getMe(): Promise<Me> {
  return requestJson<BackendMeDto>("/me");
}
