import { acceptInvite, register } from "@/lib/actions";
import { BoundForm } from "@/components/form";
import { sessionToken } from "@/lib/api";

export default async function JoinGuild({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const token = await sessionToken();
  return (
    <div className="void">
      <main className="membrane">
        <h1 className="voice">A seat in the house is open.</h1>
        {token ? (
          <BoundForm action={acceptInvite} submit="Enter">
            <input type="hidden" name="code" value={code} />
          </BoundForm>
        ) : (
          <>
            <p className="reading"><a href={`/register?code=${code}`}>Be fastened with this call</a></p>
            <BoundForm action={register} submit="Attach">
              <label>Name<input name="displayName" required /></label>
              <label>Email<input name="email" type="email" required /></label>
              <label>Password<input name="password" type="password" minLength={8} required /></label>
              <input type="hidden" name="inviteCode" value={code} />
            </BoundForm>
          </>
        )}
      </main>
    </div>
  );
}
