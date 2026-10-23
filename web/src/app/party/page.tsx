import { Users } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { completeStep, leaveParty, mintInvite, openParty, partyQuest } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function PartyPage({ searchParams }: { searchParams: Promise<{ code?: string }> }) {
  const { code } = await searchParams;
  const mine = await api<{
    party: { id: number; name: string } | null;
    quest: { name: string; status: string; milestones: Array<{ id: number; name: string; done: boolean }> } | null;
    members: Array<{ userId: string; displayName: string; fedToday: boolean; leader: boolean }>;
  }>("/parties/mine");
  const view = mine.data;
  return (
    <Shell>
      <main className="membrane">
        <h1>Gather</h1>
        <p className="voice">Call another host. A binding among hosts lasts longer.</p>
        {!view?.party && (
          <BoundForm action={openParty} submit="Open">
            <label>Name<input name="name" placeholder="A gathering" /></label>
          </BoundForm>
        )}
        {view?.party && (
          <>
            <p>{view.party.name}</p>
            {view.members.map((member) => (
              <p className="pane" key={member.userId}><Mark icon={Users} /><span>{member.fedToday ? "Fed" : "Unfed"} — {member.displayName}{member.leader ? " · leader" : ""}</span></p>
            ))}
            <BoundForm action={mintInvite} submit="Mint a fastening link">{null}</BoundForm>
            {code && <p className="reading">Share /join/{code}. An unused link pays nothing.</p>}
            <h2>Shared quest</h2>
            {view.quest && view.quest.status === "Active" ? view.quest.milestones.map((step) => (
              <div key={step.id} className="row">
                <span>{step.done ? "Fed" : "Open"} — {step.name}</span>
                {!step.done && <BoundForm action={completeStep} submit="Feed"><input type="hidden" name="id" value={step.id} /></BoundForm>}
              </div>
            )) : (
              <BoundForm action={partyQuest} submit="Open the quest">
                <label>Name<input name="name" required /></label>
                <label>Steps<textarea name="steps" required /></label>
              </BoundForm>
            )}
            <BoundForm action={leaveParty} submit="Leave">{null}</BoundForm>
          </>
        )}
      </main>
    </Shell>
  );
}
