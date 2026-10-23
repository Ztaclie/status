import { Orbit } from "lucide-react";
import { register } from "@/lib/actions";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";

export default async function Register({ searchParams }: { searchParams: Promise<{ code?: string }> }) {
  const { code } = await searchParams;
  return (
    <div className="void">
      <main className="membrane">
        <p className="reading with-icon"><Mark icon={Orbit} /> The fastening</p>
        <h1 className="voice">Name the host. This one will attach.</h1>
        <BoundForm action={register} submit="Attach">
          <label>Name<input name="displayName" required /></label>
          <label>Email<input name="email" type="email" required /></label>
          <label>Password<input name="password" type="password" minLength={8} required /></label>
          <input type="hidden" name="inviteCode" defaultValue={code ?? ""} />
        </BoundForm>
        <p className="reading"><a href="/login">The host already exists</a></p>
      </main>
    </div>
  );
}
