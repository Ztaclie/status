import { Swords } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { acceptCall, clearCall, declineCall } from "@/lib/actions";
import { api } from "@/lib/api";
import type { Me } from "@/lib/types";

export default async function Duels() {
  const me = await api<Me>("/auth/me");
  const rows = await api<Array<{ id: number; challengerId: string; opponentId: string; terms: string; deadline: string; status: string; challengerDone: boolean; opponentDone: boolean; winnerId?: string }>>("/duels");
  return (
    <Shell>
      <main className="membrane">
        <h1>Calls</h1>
        {(rows.data ?? []).length === 0 && <p className="reading">The binding is waiting.</p>}
        {(rows.data ?? []).map((duel) => (
          <div className="pane" key={duel.id}>
            <Mark icon={Swords} />
            <div>
            <p>{duel.status} — {duel.terms}</p>
            <p className="reading">Until {duel.deadline}</p>
            {duel.status === "Pending" && duel.opponentId === me.data?.id && (
              <div className="row">
                <BoundForm action={acceptCall} submit="Answer"><input type="hidden" name="id" value={duel.id} /></BoundForm>
                <BoundForm action={declineCall} submit="Refuse"><input type="hidden" name="id" value={duel.id} /></BoundForm>
              </div>
            )}
            {duel.status === "Active" && (
              <BoundForm action={clearCall} submit="Clear">
                <input type="hidden" name="id" value={duel.id} />
                <label>Note<textarea name="note" required /></label>
              </BoundForm>
            )}
            </div>
          </div>
        ))}
      </main>
    </Shell>
  );
}
