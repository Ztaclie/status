import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { passChallenge, saveChallenge, setRank } from "@/lib/actions";
import { api } from "@/lib/api";

const ranks = ["Novice", "Apprentice", "Adept", "Expert", "Master"];

export default async function SkillPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const skill = await api<{ id: number; name: string; meaning: string; rank: string; challenges: Array<{ id: number; name: string; detail: string; required: boolean; passed: boolean; rank: string; note?: string }> }>(`/skills/${id}`);
  if (!skill.data) return <Shell><p>{skill.error}</p></Shell>;
  const view = skill.data;
  const open = view.challenges.filter((c) => c.rank === view.rank && !c.passed);
  return (
    <Shell>
      <main className="membrane">
        <p className="reading">{view.rank} — {view.name}</p>
        <h1>{view.meaning || view.name}</h1>
        <p className="reading">{open.filter((c) => c.required).length} challenges remain before this faculty deepens.</p>
        {open.map((challenge) => (
          <div key={challenge.id}>
            <p>{challenge.required ? "Required" : "Optional"} — {challenge.name}</p>
            <p className="reading">{challenge.detail}</p>
            <BoundForm action={passChallenge} submit="Deepen">
              <input type="hidden" name="id" value={challenge.id} />
              <label>Note<textarea name="note" required /></label>
            </BoundForm>
          </div>
        ))}
        <h2>Write a challenge</h2>
        <BoundForm action={saveChallenge} submit="Write">
          <input type="hidden" name="skillId" value={view.id} />
          <label>Name<input name="name" required /></label>
          <label>Detail<textarea name="detail" /></label>
          <label><input type="checkbox" name="required" defaultChecked /> Required</label>
        </BoundForm>
        <h2>Set the rank by hand</h2>
        <BoundForm action={setRank} submit="Set">
          <input type="hidden" name="id" value={view.id} />
          <label>Rank<select name="rank" defaultValue={view.rank}>{ranks.map((rank) => <option key={rank}>{rank}</option>)}</select></label>
        </BoundForm>
      </main>
    </Shell>
  );
}
