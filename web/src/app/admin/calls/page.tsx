import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

export default async function Calls() {
  const rows = await api<Array<{ id: number; terms: string; status: string; deadline: string }>>("/admin/calls");
  if (rows.status === 404) return <Shell consoleMode><p>The record is not here.</p></Shell>;
  return (
    <Shell consoleMode>
      <h1>Calls</h1>
      {(rows.data ?? []).map((row) => <p key={row.id}>{row.status} · {row.terms} · {row.deadline}</p>)}
    </Shell>
  );
}
