# The Binding

A personal status window. Habits, quests, and skill ranks feed a record that stays with you. Other hosts can be seen on a public card, gathered into a party, housed in a guild, or called into a duel. Sustenance is kept. A missed day leaves a gap. It does not take the record away.

The browser talks only to the Next.js app. The Next server calls the ASP.NET API.

## Run

You need the .NET 10 SDK and Node.js.

Start the API:

```powershell
cd api/Status.Api
dotnet run
```

It listens on [http://localhost:5080](http://localhost:5080) and creates `status.db` beside the project on first launch.

Start the web app in a second terminal:

```powershell
cd web
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). Register a host from **Be fastened**. The first visit walks a short fastening: name a faculty, add a challenge, save a habit.

## Layout

- `web/` — Next.js App Router. Pages, the glass membrane, and the httpOnly session cookie.
- `api/Status.Api/` — ASP.NET Core API, Identity, and SQLite.
- `IDEA.md` — the design notes behind the window.

## Developer console

The console is not a role you can grant from the site. Put the account email in `Developer:Emails` inside [api/Status.Api/appsettings.json](api/Status.Api/appsettings.json), then register or sign in with that email. The default entry is `developer@binding.local`. A signed-in host who is not on that list gets “The record is not here” at `/admin`.

## Configuration

| Setting | Where | Default |
| --- | --- | --- |
| API address the web app calls | `API_URL` in the environment of `web/` | `http://localhost:5080` |
| SQLite file | `ConnectionStrings:Binding` | `Data Source=status.db` |
| JWT issuer | `Jwt` in `appsettings.json` | issuer and audience `binding` |
| Developer emails | `Developer:Emails` | `developer@binding.local` |

Change the JWT key before this leaves your machine. `status.db` and `vapid.json` are local and are gitignored. `vapid.json` is written by the API the first time a cue-time signal is offered.
