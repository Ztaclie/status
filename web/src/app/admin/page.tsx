import Link from "next/link";
import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

export default async function Admin() {
  const summary = await api<{ hosts: number; fed: number; thin: number; faint: number; calls: number; houses: number; gatherings: number }>("/admin");
  const hosts = await api<Array<{ id: string; displayName: string; email: string; sustenance: number }>>("/admin/hosts");
  const audit = await api<Array<{ id: number; kind: string; reason: string; before: string; after: string; targetUserId: string }>>("/admin/audit");
  if (summary.status === 404) return <Shell consoleMode><p>The record is not here.</p></Shell>;
  return (
    <Shell consoleMode>
      <h1>Developer console</h1>
      <p>{summary.data?.hosts} hosts · fed {summary.data?.fed} · thin {summary.data?.thin} · faint {summary.data?.faint} · calls {summary.data?.calls} · houses {summary.data?.houses} · gatherings {summary.data?.gatherings}</p>
      <p><Link href="/admin/gatherings">Gatherings</Link> · <Link href="/admin/houses">Houses</Link> · <Link href="/admin/calls">Calls</Link></p>
      <table>
        <tbody>
          {(hosts.data ?? []).map((host) => (
            <tr key={host.id}><td><Link href={`/admin/hosts/${host.id}`}>{host.displayName}</Link></td><td>{host.email}</td><td>{host.sustenance}</td></tr>
          ))}
        </tbody>
      </table>
      <h2>Audit</h2>
      {(audit.data ?? []).map((row) => <p key={row.id}>{row.kind} · {row.reason} · {row.before} → {row.after}</p>)}
    </Shell>
  );
}
