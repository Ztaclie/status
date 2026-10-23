import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "The Binding",
  description: "A status window fastened to its host.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
