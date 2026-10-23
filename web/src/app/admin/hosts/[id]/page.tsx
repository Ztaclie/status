import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { adminFame, adminItem, adminLock, adminTitle } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function AdminHost({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const host = await api<{
    status: { displayName: string; bindingState: string; sustenance: number };
    conditionLabel: string; conditionNote: string;
    notes: Array<{ name: string; note: string }>;
    ledger: Array<{ amount: number; reason: string }>;
    log: Array<{ body: string }>;
    locked: boolean;
  }>(`/admin/hosts/${id}`);
  if (host.status === 404) return <Shell consoleMode><p>The record is not here.</p></Shell>;
  const view = host.data;
  if (!view) return <Shell consoleMode><p>{host.error}</p></Shell>;
  return (
    <Shell consoleMode>
      <h1>{view.status.displayName}</h1>
      <p>{view.status.bindingState} · sustenance {view.status.sustenance} · {view.locked ? "locked" : "open"}</p>
      <p>{view.conditionLabel} {view.conditionNote}</p>
      <h2>Notes</h2>
      {view.notes.map((note, i) => <p key={i}>{note.name}: {note.note}</p>)}
      <h2>Ledger</h2>
      {view.ledger.map((row, i) => <p key={i}>{row.amount} {row.reason}</p>)}
      <h2>Log</h2>
      {view.log.slice(0, 12).map((row, i) => <p key={i}>{row.body}</p>)}
      <BoundForm action={adminFame} submit="Adjust sustenance">
        <input type="hidden" name="id" value={id} />
        <label>Amount<input name="amount" type="number" required /></label>
        <label>Reason<input name="reason" required /></label>
      </BoundForm>
      <BoundForm action={adminTitle} submit="Grant a name">
        <input type="hidden" name="id" value={id} />
        <label>Name<input name="text" required /></label>
        <label>Reason<input name="reason" required /></label>
      </BoundForm>
      <BoundForm action={adminItem} submit="Grant a thing">
        <input type="hidden" name="id" value={id} />
        <label>Key<select name="itemKey"><option>first-feeding</option><option>pulse</option><option>shared-tether</option><option>house-sigil</option><option>answered-call</option></select></label>
        <label>Reason<input name="reason" required /></label>
      </BoundForm>
      <BoundForm action={adminLock} submit={view.locked ? "Unlock" : "Disable sign-in"}>
        <input type="hidden" name="id" value={id} />
        <input type="hidden" name="locked" value={view.locked ? "false" : "true"} />
        <label>Reason<input name="reason" required /></label>
      </BoundForm>
    </Shell>
  );
}
