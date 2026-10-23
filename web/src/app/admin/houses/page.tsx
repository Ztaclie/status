import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

export default async function Houses() {
  const rows = await api<Array<{ id: number; name: string; creed: string; members: Array<{ userId: string }> }>>("/admin/houses");
  if (rows.status === 404) return <Shell consoleMode><p>The record is not here.</p></Shell>;
  return (
    <Shell consoleMode>
      <h1>Houses</h1>
      {(rows.data ?? []).map((row) => <p key={row.id}>{row.name} — {row.creed} · {row.members.length}</p>)}
    </Shell>
  );
}
