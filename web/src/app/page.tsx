import { Calendar, Circle, CircleDot, CloudMoon, Droplets, Hourglass, Moon, ScrollText, Sparkles, Sun } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { SignalAsk } from "@/components/signal";
import { acceptInvite, completeStep, feed, writeLine } from "@/lib/actions";
import { api } from "@/lib/api";
import type { Me, Status } from "@/lib/types";

export default async function Home() {
  const status = await api<Status>("/status");
  const me = await api<Me>("/auth/me");
  if (!status.data) return <Shell><p className="voice">{status.error}</p></Shell>;
  const view = status.data;
  const act = view.keystone?.useTwoMinute ? view.keystone.twoMinute : (view.keystone?.fullVersion || view.keystone?.twoMinute);
  const stateIcon = view.bindingState === "Fed" ? Sun : view.bindingState === "Thin" ? CloudMoon : view.bindingState === "Faint" ? Moon : Hourglass;
  return (
    <Shell>
      <main className={`membrane ${view.bindingState}`}>
        <div className="chips">
          <span className="chip"><Mark icon={stateIcon} /> {view.bindingState}</span>
          <span className="chip"><Mark icon={Calendar} /> Day {view.dayOfBinding} of the binding</span>
          <span className="chip"><Mark icon={Droplets} /> sustenance {view.sustenance}</span>
        </div>
        <h1>{view.displayName}</h1>
        <p className="voice">{view.title || "No name is worn."}</p>
        <p className="reading">{view.conditionLabel} {view.conditionNote}</p>
        <p className="reading">{view.arcName} {view.arcNote}</p>
        <p className="reading">{view.runLine} · {view.lifetimeFeedings} feedings</p>
        {view.keystone && (
          <section>
            <p className="voice">{view.keystone.identity}</p>
            <p className="reading">{view.keystone.cue} · {view.keystone.cueTime}</p>
            <p>{act}</p>
            <div className="strip">{view.keystone.marks.map((on, i) => <Mark key={i} icon={on ? CircleDot : Circle} size={14} className={on ? "on" : ""} />)}</div>
            {!view.keystone.fedToday && (
              <BoundForm action={feed} submit="Feed">
                <input type="hidden" name="id" value={view.keystone.id} />
              </BoundForm>
            )}
          </section>
        )}
        {view.leaveLine && <p className="voice">{view.leaveLine}</p>}
        {view.askForSignal && me.data && <SignalAsk vapid={me.data.vapidPublicKey} />}
        {view.fasteningOpen && (
          <section>
            <h2>This One Attaches</h2>
            {view.fasteningSteps.map((step) => <p key={step.id} className="reading">{step.done ? "Fed" : "Waiting"} — {step.name}</p>)}
          </section>
        )}
        {view.pendingSeat && (
          <section>
            <p className="voice">{view.pendingSeat.fromName} called you into a {view.pendingSeat.kind === "Guild" ? "house" : "gathering"}.</p>
            {view.pendingSeat.canSeat && (
              <BoundForm action={acceptInvite} submit="Take the seat">
                <input type="hidden" name="code" value={view.pendingSeat.code} />
              </BoundForm>
            )}
          </section>
        )}
        <section>
          <h2 className="with-icon"><Mark icon={Sparkles} /> Faculties</h2>
          {view.faculties.map((skill) => (
            <p key={skill.id}><a href={`/skills/${skill.id}`}>{skill.label}</a> <span className="reading">{skill.challengesRemaining} challenges remain</span></p>
          ))}
          {view.faculties.length === 0 && <p className="reading">The binding is waiting for a faculty.</p>}
        </section>
        <section>
          <h2 className="with-icon"><Mark icon={ScrollText} /> Main quest</h2>
          {view.mainQuest ? view.mainQuest.steps.map((step) => (
            <div key={step.id} className="row">
              <span>{step.done ? "Fed" : "Open"} — {step.name}</span>
              {!step.done && (
                <BoundForm action={completeStep} submit="Feed">
                  <input type="hidden" name="id" value={step.id} />
                </BoundForm>
              )}
            </div>
          )) : <p className="reading">No main quest.</p>}
        </section>
        <section>
          <h2 className="with-icon"><Mark icon={Sun} /> Today</h2>
          {view.dailies.map((daily) => (
            <div key={daily.id}>
              <p>{daily.done ? "Fed" : "Unfed"} — {daily.text}</p>
              {!daily.done && daily.kind === "Line" && (
                <BoundForm action={writeLine} submit="Give the line">
                  <input type="hidden" name="id" value={daily.id} />
                  <label>The line<textarea name="note" required /></label>
                </BoundForm>
              )}
            </div>
          ))}
        </section>
        {view.wornThing && <p className="reading">Worn: {view.wornThing}</p>}
        {view.recentNames.length > 0 && <p className="reading">{view.recentNames.join(" · ")}</p>}
        {view.lines.map((line, i) => <p key={i} className="voice">{line}</p>)}
      </main>
    </Shell>
  );
}
