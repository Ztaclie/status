import { Castle } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { completeStep, guildQuest, leaveGuild, mintGuild, openGuild } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function Guilds({ searchParams }: { searchParams: Promise<{ code?: string }> }) {
  const { code } = await searchParams;
  const mine = await api<{
    guild: { id: number; name: string; creed: string; keeperId: string } | null;
    quest: { status: string; milestones: Array<{ id: number; name: string; done: boolean }> } | null;
    members: Array<{ userId: string; displayName: string; fedToday: boolean; leader: boolean }>;
    line: string | null;
  }>("/guilds/mine");
  const view = mine.data;
  const fed = view?.members.filter((m) => m.fedToday).length ?? 0;
  return (
    <Shell>
      <main className="membrane">
        <h1>House</h1>
        <p className="voice">A gathering is near. A house is wider.</p>
        {!view?.guild && (
          <BoundForm action={openGuild} submit="Raise">
            <label>Name<input name="name" required /></label>
            <label>Creed<input name="creed" required /></label>
          </BoundForm>
        )}
        {view?.guild && (
          <>
            <p>{view.guild.name}</p>
            <p className="reading">{view.guild.creed}</p>
            <p className="voice">{view.line ?? `${fed} hosts fed the house today.`}</p>
            {view.members.map((member) => (
              <p className="pane" key={member.userId}><Mark icon={Castle} /><span>{member.fedToday ? "Fed — " : ""}{member.displayName}{member.leader ? " · keeper" : ""}</span></p>
            ))}
            <BoundForm action={mintGuild} submit="Open a seat">
              <input type="hidden" name="id" value={view.guild.id} />
            </BoundForm>
            {code && <p className="reading">Share /guilds/join/{code}</p>}
            <h2>House quest</h2>
            {view.quest && view.quest.status === "Active" ? view.quest.milestones.map((step) => (
              <div key={step.id} className="row">
                <span>{step.done ? "Fed" : "Open"} — {step.name}</span>
                {!step.done && <BoundForm action={completeStep} submit="Feed"><input type="hidden" name="id" value={step.id} /></BoundForm>}
              </div>
            )) : (
              <BoundForm action={guildQuest} submit="Open the quest">
                <input type="hidden" name="guildId" value={view.guild.id} />
                <label>Name<input name="name" required /></label>
                <label>Steps<textarea name="steps" required /></label>
              </BoundForm>
            )}
            <BoundForm action={leaveGuild} submit="Leave">{null}</BoundForm>
          </>
        )}
      </main>
    </Shell>
  );
}
