import { Gem } from "lucide-react";
import { Shell } from "@/components/shell";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";
import { equip } from "@/lib/actions";
import { api } from "@/lib/api";

export default async function Inventory() {
  const items = await api<Array<{ id: number; name: string; description: string; quantity: number; wearable: boolean; equipped: boolean }>>("/inventory");
  return (
    <Shell>
      <main className="membrane">
        <h1>What it grew</h1>
        {(items.data ?? []).length === 0 && <p className="reading">The binding is waiting.</p>}
        {(items.data ?? []).map((item) => (
          <div className="pane" key={item.id}>
            <Mark icon={Gem} />
            <div>
              <p>{item.equipped ? "Worn" : "Kept"} — {item.name} × {item.quantity}</p>
              <p className="reading">{item.description}</p>
              {item.wearable && !item.equipped && (
                <BoundForm action={equip} submit="Wear"><input type="hidden" name="id" value={item.id} /></BoundForm>
              )}
            </div>
          </div>
        ))}
      </main>
    </Shell>
  );
}
