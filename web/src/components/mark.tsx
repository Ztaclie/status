import type { LucideIcon } from "lucide-react";

export function Mark({ icon: Icon, size = 16, className = "" }: { icon: LucideIcon; size?: number; className?: string }) {
  return <Icon className={`mark-icon ${className}`.trim()} size={size} strokeWidth={1.5} aria-hidden />;
}
