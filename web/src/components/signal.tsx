"use client";

import { declineSignal, subscribePush } from "@/lib/actions";
import { BoundForm } from "@/components/form";

function keyBytes(value: string) {
  const padding = "=".repeat((4 - (value.length % 4)) % 4);
  const base = (value + padding).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base);
  return Uint8Array.from([...raw].map((char) => char.charCodeAt(0)));
}

export function SignalAsk({ vapid }: { vapid: string }) {
  return (
    <div className="stack">
      <p className="voice">This one can call you at the cue. Once.</p>
      <div className="row">
        <button
          type="button"
          onClick={async () => {
            const registration = await navigator.serviceWorker.register("/sw.js");
            const subscription = await registration.pushManager.subscribe({
              userVisibleOnly: true,
              applicationServerKey: keyBytes(vapid),
            });
            const json = subscription.toJSON();
            await subscribePush({
              endpoint: subscription.endpoint,
              p256dh: json.keys?.p256dh ?? "",
              auth: json.keys?.auth ?? "",
            });
          }}
        >
          Allow the call
        </button>
        <BoundForm action={declineSignal} submit="Remain silent">{null}</BoundForm>
      </div>
    </div>
  );
}
