import { cookies } from "next/headers";

const root = process.env.API_URL ?? "http://localhost:5080";

export type ApiResult<T> = { data?: T; error?: string; status: number };

export async function api<T>(path: string, init?: RequestInit): Promise<ApiResult<T>> {
  const jar = await cookies();
  const token = jar.get("binding_session")?.value;
  const headers = new Headers(init?.headers);
  if (init?.body) headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);
  const response = await fetch(`${root}${path}`, { ...init, headers, cache: "no-store" });
  const text = await response.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!response.ok) return { error: data?.error ?? "The binding did not answer.", status: response.status };
  return { data: data as T, status: response.status };
}

export async function sessionToken() {
  return (await cookies()).get("binding_session")?.value;
}
