import { Orbit } from "lucide-react";
import { login } from "@/lib/actions";
import { BoundForm } from "@/components/form";
import { Mark } from "@/components/mark";

export default function Login() {
  return (
    <div className="void">
      <main className="membrane">
        <p className="reading with-icon"><Mark icon={Orbit} /> The fastening</p>
        <h1 className="voice">Something from outside the sky is looking for a host.</h1>
        <BoundForm action={login} submit="Enter">
          <label>Email<input name="email" type="email" required /></label>
          <label>Password<input name="password" type="password" required /></label>
        </BoundForm>
        <p className="reading"><a href="/register">Be fastened</a></p>
      </main>
    </div>
  );
}
