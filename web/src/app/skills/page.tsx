import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { saveSkill } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function Skills() {
  const skills = await api<Array<{ id: number; name: string; meaning: string; rank: string }>>("/skills");
  return (
    <Shell>
      <main className="membrane">
        <h1>Faculties</h1>
        <BoundForm action={saveSkill} submit="Name">
          <label>Name<input name="name" required placeholder="Coding" /></label>
          <label>Meaning<textarea name="meaning" /></label>
        </BoundForm>
        {(skills.data ?? []).map((skill) => (
          <p key={skill.id}><a href={`/skills/${skill.id}`}>{skill.rank} — {skill.name}</a></p>
        ))}
      </main>
    </Shell>
  );
}
