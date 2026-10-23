import { Castle, User } from "lucide-react";
import { Shell } from "@/components/shell";
import { Mark } from "@/components/mark";
import { api } from "@/lib/api";
import type { Card } from "@/lib/types";

export default async function Leaderboard() {
  const hosts = await api<Card[]>("/leaderboard");
  const houses = await api<Array<{ id: number; name: string; creed: string; members: number; weeklyFeedings: number }>>("/guilds");
  return (
    <Shell>
      <main className="membrane">
        <h1>The register of the bound</h1>
        {(hosts.data ?? []).map((host) => (
          <p className="pane" key={host.id}><Mark icon={User} /><span><a href={`/players/${host.id}`}>{host.displayName}</a> <span className="reading">{host.sustenance} · {host.title} · {host.faculties[0]}</span></span></p>
        ))}
        <h2>The register of houses</h2>
        {(houses.data ?? []).map((house) => (
          <p className="pane" key={house.id}><Mark icon={Castle} /><span>{house.name} <span className="reading">{house.weeklyFeedings} feedings this week · {house.members} hosts · {house.creed}</span></span></p>
        ))}
      </main>
    </Shell>
  );
}
