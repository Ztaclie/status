import { acceptInvite, register } from "@/lib/actions";
import { BoundForm } from "@/components/form";
import { sessionToken } from "@/lib/api";

export default async function Join({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const token = await sessionToken();
  return (
    <div className="void">
      <main className="membrane">
        <h1 className="voice">You were called.</h1>
        {token ? (
          <BoundForm action={acceptInvite} submit="Take the seat">
            <input type="hidden" name="code" value={code} />
          </BoundForm>
        ) : (
          <BoundForm action={register} submit="Attach">
            <label>Name<input name="displayName" required /></label>
            <label>Email<input name="email" type="email" required /></label>
            <label>Password<input name="password" type="password" minLength={8} required /></label>
            <input type="hidden" name="inviteCode" value={code} />
          </BoundForm>
        )}
      </main>
    </div>
  );
}
