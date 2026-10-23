"use server";

import { revalidatePath } from "next/cache";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { api } from "./api";

async function remember(token: string) {
  const jar = await cookies();
  jar.set("binding_session", token, { httpOnly: true, sameSite: "lax", path: "/", maxAge: 60 * 60 * 24 * 14 });
}

function done(path = "/") {
  revalidatePath(path, "layout");
}

export async function login(form: FormData) {
  const response = await fetch(`${process.env.API_URL ?? "http://localhost:5080"}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: form.get("email"), password: form.get("password") }),
  });
  const data = await response.json();
  if (!response.ok) return { error: data.error as string };
  await remember(data.token);
  redirect("/");
}

export async function register(form: FormData) {
  const response = await fetch(`${process.env.API_URL ?? "http://localhost:5080"}/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email: form.get("email"),
      password: form.get("password"),
      displayName: form.get("displayName"),
      inviteCode: form.get("inviteCode") || null,
    }),
  });
  const data = await response.json();
  if (!response.ok) return { error: data.error as string };
  await remember(data.token);
  redirect("/");
}

export async function logout() {
  const jar = await cookies();
  jar.delete("binding_session");
  redirect("/login");
}

async function post(path: string, body?: unknown) {
  const result = await api(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });
  return result.error ? { error: result.error } : {};
}

export async function feed(form: FormData) {
  const result = await post(`/habits/${form.get("id")}/show-up`);
  done();
  return result;
}
export async function saveHabit(form: FormData) {
  const days = form.getAll("day").map(Number).reduce((sum, bit) => sum + bit, 0);
  const result = await post("/habits", {
    identity: form.get("identity"), cue: form.get("cue"), cueTime: form.get("cueTime"),
    twoMinute: form.get("twoMinute"), fullVersion: form.get("fullVersion"),
    weekdays: days || 127, keystone: form.get("keystone") === "on",
  });
  done("/quests");
  return result;
}
export async function retireHabit(form: FormData) {
  const result = await post(`/habits/${form.get("id")}/retire`);
  done("/quests");
  return result;
}
export async function saveQuest(form: FormData) {
  const steps = String(form.get("steps") ?? "").split("\n").map((s) => s.trim()).filter(Boolean);
  const result = await post("/quests", { name: form.get("name"), detail: form.get("detail"), isMain: form.get("main") === "on", steps });
  done("/quests");
  return result;
}
export async function completeStep(form: FormData) {
  const result = await post(`/milestones/${form.get("id")}/complete`);
  done();
  return result;
}
export async function dropQuest(form: FormData) {
  const result = await post(`/quests/${form.get("id")}/drop`);
  done("/quests");
  return result;
}
export async function makeMain(form: FormData) {
  const result = await post(`/quests/${form.get("id")}/make-main`);
  done("/quests");
  return result;
}
export async function saveSkill(form: FormData) {
  const result = await post("/skills", { name: form.get("name"), meaning: form.get("meaning") });
  done("/skills");
  return result;
}
export async function saveChallenge(form: FormData) {
  const result = await post(`/skills/${form.get("skillId")}/challenges`, { name: form.get("name"), detail: form.get("detail"), required: form.get("required") === "on" });
  done(`/skills/${form.get("skillId")}`);
  return result;
}
export async function passChallenge(form: FormData) {
  const result = await post(`/challenges/${form.get("id")}/pass`, { note: form.get("note") });
  done();
  return result;
}
export async function setRank(form: FormData) {
  const result = await api(`/skills/${form.get("id")}`, { method: "PUT", body: JSON.stringify({ rank: form.get("rank") }) });
  done(`/skills/${form.get("id")}`);
  return result.error ? { error: result.error } : {};
}
export async function saveGoal(form: FormData) {
  const result = await post("/achievements", { name: form.get("name"), detail: form.get("detail") });
  done("/achievements");
  return result;
}
export async function claimGoal(form: FormData) {
  const result = await post(`/achievements/${form.get("id")}/claim`);
  done("/achievements");
  return result;
}
export async function equip(form: FormData) {
  const result = await post(`/inventory/${form.get("id")}/equip`);
  done("/inventory");
  return result;
}
export async function writeLine(form: FormData) {
  const result = await post(`/dailies/${form.get("id")}/line`, { note: form.get("note") });
  done();
  return result;
}
export async function saveProfile(form: FormData) {
  const result = await api("/profile", { method: "PUT", body: JSON.stringify({
    displayName: form.get("displayName"), conditionLabel: form.get("conditionLabel"), conditionNote: form.get("conditionNote"),
    timeZone: form.get("timeZone"), arcName: form.get("arcName"), arcNote: form.get("arcNote"),
  }) });
  done("/edit");
  return result.error ? { error: result.error } : {};
}
export async function wearTitle(form: FormData) {
  const result = await post("/titles", { titleId: form.get("titleId") ? Number(form.get("titleId")) : null, text: form.get("text") || null });
  done("/edit");
  return result;
}
export async function openParty(form: FormData) {
  const result = await post("/parties", { name: form.get("name") });
  done("/party");
  return result;
}
export async function mintInvite(_form: FormData) {
  const result = await api<{ code: string }>("/invites", { method: "POST" });
  if (result.error) return { error: result.error };
  redirect(`/party?code=${result.data?.code ?? ""}`);
}
export async function acceptInvite(form: FormData) {
  const result = await post(`/invites/${form.get("code")}/accept`);
  done();
  return result;
}
export async function partyQuest(form: FormData) {
  const steps = String(form.get("steps") ?? "").split("\n").map((s) => s.trim()).filter(Boolean);
  const result = await post("/parties/quests", { name: form.get("name"), steps });
  done("/party");
  return result;
}
export async function leaveParty(_form: FormData) {
  const result = await post("/parties/leave");
  done("/party");
  return result;
}
export async function openGuild(form: FormData) {
  const result = await post("/guilds", { name: form.get("name"), creed: form.get("creed") });
  done("/guilds");
  return result;
}
export async function mintGuild(form: FormData) {
  const result = await api<{ code: string }>(`/guilds/${form.get("id")}/invites`, { method: "POST" });
  if (result.error) return { error: result.error };
  redirect(`/guilds?code=${result.data?.code ?? ""}`);
}
export async function guildQuest(form: FormData) {
  const steps = String(form.get("steps") ?? "").split("\n").map((s) => s.trim()).filter(Boolean);
  const result = await post("/guilds/quests", { guildId: Number(form.get("guildId")), name: form.get("name"), steps });
  done("/guilds");
  return result;
}
export async function leaveGuild(_form: FormData) {
  const result = await post("/guilds/leave");
  done("/guilds");
  return result;
}
export async function callHost(form: FormData) {
  const result = await post("/duels", { opponentId: form.get("opponentId"), terms: form.get("terms"), deadline: form.get("deadline") });
  done("/duels");
  return result;
}
export async function acceptCall(form: FormData) {
  const result = await post(`/duels/${form.get("id")}/accept`);
  done("/duels");
  return result;
}
export async function declineCall(form: FormData) {
  const result = await post(`/duels/${form.get("id")}/decline`);
  done("/duels");
  return result;
}
export async function clearCall(form: FormData) {
  const result = await post(`/duels/${form.get("id")}/clear`, { note: form.get("note") });
  done("/duels");
  return result;
}
export async function subscribePush(body: { endpoint: string; p256dh: string; auth: string }) {
  const result = await post("/push/subscribe", { endpoint: body.endpoint, p256dh: body.p256dh, auth: body.auth });
  done();
  return result;
}
export async function declineSignal(_form: FormData) {
  const result = await post("/push/decline");
  done();
  return result;
}
export async function adminLock(form: FormData) {
  const result = await post(`/admin/hosts/${form.get("id")}/lock`, { locked: form.get("locked") === "true", reason: form.get("reason") });
  done("/admin");
  return result;
}
export async function adminFame(form: FormData) {
  const result = await post(`/admin/hosts/${form.get("id")}/sustenance`, { amount: Number(form.get("amount")), reason: form.get("reason") });
  done("/admin");
  return result;
}
export async function adminTitle(form: FormData) {
  const result = await post(`/admin/hosts/${form.get("id")}/titles`, { text: form.get("text"), reason: form.get("reason") });
  done("/admin");
  return result;
}
export async function adminItem(form: FormData) {
  const result = await post(`/admin/hosts/${form.get("id")}/items`, { itemKey: form.get("itemKey"), reason: form.get("reason") });
  done("/admin");
  return result;
}
