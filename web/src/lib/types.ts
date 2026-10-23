export type Step = { id: number; name: string; done: boolean };
export type Skill = { id: number; name: string; meaning: string; rank: string; label: string; challengesRemaining: number };
export type Quest = { id: number; name: string; detail: string; steps: Step[] };
export type Daily = { id: number; text: string; kind: string; done: boolean };
export type Keystone = {
  id: number; identity: string; cue: string; cueTime: string; twoMinute: string; fullVersion: string;
  useTwoMinute: boolean; marks: boolean[]; fedToday: boolean;
};
export type Status = {
  bindingState: string; displayName: string; title?: string; conditionLabel?: string; conditionNote?: string;
  arcName?: string; arcNote?: string; sustenance: number; dayOfBinding: number; lifetimeFeedings: number;
  runDays: number; runLine: string; keystone?: Keystone; askForSignal: boolean; fasteningOpen: boolean;
  fasteningSteps: Step[]; faculties: Skill[]; mainQuest?: Quest; dailies: Daily[]; recentNames: string[];
  wornThing?: string; lines: string[]; leaveLine?: string;
  pendingSeat?: { code: string; kind: string; fromName: string; canSeat: boolean }; fedToday: boolean;
};
export type Me = { id: string; displayName: string; email: string; isDeveloper: boolean; tutorialComplete: boolean; signalOffered: boolean; vapidPublicKey: string };
export type Card = { id: string; displayName: string; dayOfBinding: number; title?: string; sustenance: number; faculties: string[]; names: string[]; worn?: string; gathering?: string; house?: string };
