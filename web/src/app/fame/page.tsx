import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

export default async function Fame() {
  const rows = await api<Array<{ id: number; amount: number; reason: string; createdAt: string }>>("/fame");
  return (
    <Shell>
      <main className="membrane">
        <h1>Sustenance</h1>
        {(rows.data ?? []).map((row) => <p key={row.id}>{row.amount > 0 ? "+" : ""}{row.amount} — {row.reason}</p>)}
      </main>
    </Shell>
  );
}
