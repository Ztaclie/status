"use client";

import { useActionState } from "react";

export function BoundForm({
  action,
  children,
  submit,
}: {
  action: (form: FormData) => Promise<{ error?: string } | void>;
  children: React.ReactNode;
  submit: string;
}) {
  const [state, formAction, pending] = useActionState(async (_prev: { error?: string } | null, form: FormData) => {
    const result = await action(form);
    return result ?? null;
  }, null);
  return (
    <form className="stack" action={formAction}>
      {children}
      {state?.error && <p className="error">{state.error}</p>}
      <button disabled={pending}>{submit}</button>
    </form>
  );
}
