import { ScrollText } from "lucide-react";
import { Shell } from "@/components/shell";
import { Mark } from "@/components/mark";
import { api } from "@/lib/api";

export default async function Log() {
  const rows = await api<Array<{ id: number; body: string; createdAt: string }>>("/log");
  return (
    <Shell>
      <main className="membrane">
        <h1>The log</h1>
        {(rows.data ?? []).map((row) => <p key={row.id} className="pane voice"><Mark icon={ScrollText} /><span>{row.body}</span></p>)}
      </main>
    </Shell>
  );
}
