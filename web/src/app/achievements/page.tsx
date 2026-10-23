import { Award } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { claimGoal, saveGoal } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function Achievements() {
  const rows = await api<Array<{ id: number; name: string; detail: string; earned: boolean; isHidden: boolean }>>("/achievements");
  return (
    <Shell>
      <main className="membrane">
        <h1>Names</h1>
        <BoundForm action={saveGoal} submit="Name a goal">
          <label>Name<input name="name" required /></label>
          <label>Detail<textarea name="detail" /></label>
        </BoundForm>
        {(rows.data ?? []).map((row) => (
          <div className="pane" key={row.id}>
            <Mark icon={Award} />
            <div>
              <p>{row.earned ? "Opened" : "Waiting"} — {row.name}</p>
              <p className="reading">{row.detail}</p>
              {!row.earned && !row.isHidden && (
                <BoundForm action={claimGoal} submit="Claim"><input type="hidden" name="id" value={row.id} /></BoundForm>
              )}
            </div>
          </div>
        ))}
      </main>
    </Shell>
  );
}
