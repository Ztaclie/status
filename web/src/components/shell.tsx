import Link from "next/link";
import { Activity, Award, Castle, Droplets, Gem, ListChecks, LogOut, PenLine, ScrollText, Sparkles, Swords, Terminal, Trophy, Users } from "lucide-react";
import { logout } from "@/lib/actions";
import { api } from "@/lib/api";
import { Mark } from "@/components/mark";
import type { Me } from "@/lib/types";

export async function Shell({ children, consoleMode = false }: { children: React.ReactNode; consoleMode?: boolean }) {
  const me = await api<Me>("/auth/me");
  return (
    <div className="void">
      <nav>
        <Link href="/"><Mark icon={Activity} /> Status</Link>
        <Link href="/quests"><Mark icon={ListChecks} /> Quests</Link>
        <Link href="/skills"><Mark icon={Sparkles} /> Faculties</Link>
        <Link href="/achievements"><Mark icon={Award} /> Names</Link>
        <Link href="/inventory"><Mark icon={Gem} /> Grown</Link>
        <Link href="/leaderboard"><Mark icon={Trophy} /> Register</Link>
        <Link href="/party"><Mark icon={Users} /> Gather</Link>
        <Link href="/guilds"><Mark icon={Castle} /> House</Link>
        <Link href="/duels"><Mark icon={Swords} /> Calls</Link>
        <Link href="/log"><Mark icon={ScrollText} /> Log</Link>
        <Link href="/fame"><Mark icon={Droplets} /> Sustenance</Link>
        <Link href="/edit"><Mark icon={PenLine} /> Rewrite</Link>
        {me.data?.isDeveloper && <Link href="/admin"><Mark icon={Terminal} /> Console</Link>}
        <form action={logout}><button><Mark icon={LogOut} /> Leave</button></form>
      </nav>
      {consoleMode ? <div className="console">{children}</div> : children}
    </div>
  );
}
