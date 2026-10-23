import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

export default async function Gatherings() {
  const rows = await api<Array<{ id: number; name: string; leaderId: string; members: Array<{ userId: string }> }>>("/admin/gatherings");
  if (rows.status === 404) return <Shell consoleMode><p>The record is not here.</p></Shell>;
  return (
    <Shell consoleMode>
      <h1>Gatherings</h1>
      {(rows.data ?? []).map((row) => <p key={row.id}>{row.name} · {row.members.length} hosts</p>)}
    </Shell>
  );
}
