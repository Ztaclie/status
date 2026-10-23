import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
  const token = request.cookies.get("binding_session");
  const path = request.nextUrl.pathname;
  const open = path === "/login" || path === "/register" || path.startsWith("/join/") || path.startsWith("/guilds/join/");
  if (!token && !open) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }
  return NextResponse.next();
}

export const config = { matcher: ["/((?!_next|favicon.ico|sw.js).*)"] };
