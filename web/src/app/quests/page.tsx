import { Flag, Repeat } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { dropQuest, makeMain, retireHabit, saveHabit, saveQuest } from "@/lib/actions";
import { api } from "@/lib/api";

const days = [["Mon", 1], ["Tue", 2], ["Wed", 4], ["Thu", 8], ["Fri", 16], ["Sat", 32], ["Sun", 64]];

export default async function Quests() {
  const habits = await api<Array<{ id: number; identityLine: string; cue: string; cueTime: string; twoMinute: string; isKeystone: boolean; status: string }>>("/habits");
  const quests = await api<Array<{ id: number; name: string; detail: string; isMain: boolean; status: string; milestones: Array<{ id: number; name: string; done: boolean }> }>>("/quests");
  return (
    <Shell>
      <main className="membrane">
        <h1>Quests</h1>
        <h2>A habit</h2>
        <BoundForm action={saveHabit} submit="Fasten">
          <label>Identity<input name="identity" required placeholder="I am someone who sits down to write." /></label>
          <label>Cue<input name="cue" required /></label>
          <label>Cue time<input name="cueTime" type="time" required /></label>
          <label>Two minutes<textarea name="twoMinute" required /></label>
          <label>Full version<textarea name="fullVersion" /></label>
          <div className="row">{days.map(([label, bit]) => <label key={label}><input type="checkbox" name="day" value={bit} defaultChecked /> {label}</label>)}</div>
          <label><input type="checkbox" name="keystone" /> Keystone</label>
        </BoundForm>
        {(habits.data ?? []).map((habit) => (
          <div className="pane" key={habit.id}>
            <Mark icon={Repeat} />
            <div>
              <p>{habit.isKeystone ? "Keystone" : "Habit"} — {habit.identityLine}</p>
              <p className="reading">{habit.cue} · {habit.cueTime} · {habit.status}</p>
              {habit.status === "Active" && (
                <BoundForm action={retireHabit} submit="Set down">
                  <input type="hidden" name="id" value={habit.id} />
                </BoundForm>
              )}
            </div>
          </div>
        ))}
        <h2>A milestone quest</h2>
        <BoundForm action={saveQuest} submit="Open">
          <label>Name<input name="name" required /></label>
          <label>Detail<textarea name="detail" /></label>
          <label>Steps, one a line<textarea name="steps" required /></label>
          <label><input type="checkbox" name="main" /> Main quest</label>
        </BoundForm>
        {(quests.data ?? []).map((quest) => (
          <div className="pane" key={quest.id}>
            <Mark icon={Flag} />
            <div>
              <p>{quest.isMain ? "Main" : "Side"} — {quest.name} · {quest.status}</p>
              {quest.milestones.map((step) => <p key={step.id} className="reading">{step.done ? "Fed" : "Open"} — {step.name}</p>)}
              <div className="row">
                {quest.status === "Active" && !quest.isMain && <BoundForm action={makeMain} submit="Make main"><input type="hidden" name="id" value={quest.id} /></BoundForm>}
                {quest.status === "Active" && <BoundForm action={dropQuest} submit="Set down"><input type="hidden" name="id" value={quest.id} /></BoundForm>}
              </div>
            </div>
          </div>
        ))}
      </main>
    </Shell>
  );
}
