import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { callHost } from "@/lib/actions";
import { api } from "@/lib/api";
import type { Card } from "@/lib/types";

export default async function Player({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const card = await api<Card>(`/players/${id}`);
  if (!card.data) return <Shell><p>{card.error}</p></Shell>;
  const view = card.data;
  return (
    <Shell>
      <main className="membrane">
        <p className="reading">Day {view.dayOfBinding} of the binding · sustenance {view.sustenance}</p>
        <h1>{view.displayName}</h1>
        <p className="voice">{view.title || "No name is worn."}</p>
        {view.faculties.map((faculty) => <p key={faculty}>{faculty}</p>)}
        <p className="reading">{view.names.join(" · ")}</p>
        {view.worn && <p className="reading">Worn: {view.worn}</p>}
        {view.gathering && <p className="reading">Gathering: {view.gathering}</p>}
        {view.house && <p className="reading">House: {view.house}</p>}
        <h2>Call</h2>
        <BoundForm action={callHost} submit="Call">
          <input type="hidden" name="opponentId" value={view.id} />
          <label>Terms<textarea name="terms" required /></label>
          <label>Deadline<input name="deadline" type="date" required /></label>
        </BoundForm>
      </main>
    </Shell>
  );
}
