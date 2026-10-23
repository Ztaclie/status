import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { saveProfile, wearTitle } from "@/lib/actions";
import { api } from "@/lib/api";
import type { Status } from "@/lib/types";

export default async function Edit() {
  const status = await api<Status>("/status");
  const titles = await api<Array<{ id: number; text: string; isActive: boolean }>>("/titles");
  const view = status.data;
  return (
    <Shell>
      <main className="membrane">
        <h1>Rewrite</h1>
        <BoundForm action={saveProfile} submit="Rewrite">
          <label>Name<input name="displayName" defaultValue={view?.displayName} /></label>
          <label>Condition<input name="conditionLabel" defaultValue={view?.conditionLabel} /></label>
          <label>Note<textarea name="conditionNote" defaultValue={view?.conditionNote} /></label>
          <label>Timezone<input name="timeZone" defaultValue="UTC" /></label>
          <label>Arc<input name="arcName" defaultValue={view?.arcName} /></label>
          <label>Arc note<textarea name="arcNote" defaultValue={view?.arcNote} /></label>
        </BoundForm>
        <h2>Wear a name</h2>
        {(titles.data ?? []).map((title) => (
          <BoundForm key={title.id} action={wearTitle} submit={title.isActive ? "Worn" : "Wear"}>
            <input type="hidden" name="titleId" value={title.id} />
            <span>{title.text}</span>
          </BoundForm>
        ))}
        <BoundForm action={wearTitle} submit="Speak a new name">
          <label>Name<input name="text" required /></label>
        </BoundForm>
      </main>
    </Shell>
  );
}
