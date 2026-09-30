# Public booking page

This folder is a static GitHub Pages site. It contains only the Georgian booking form. It does not contain the Tutorplanner admin application or the SQLite database.

## Before publishing

1. Put the HTTPS URL of the public API in `index.html` by replacing `https://YOUR-TUNNEL-DOMAIN.example/api/public-booking`.
2. Configure the local app's `TUTORPLANNER_PUBLIC_ORIGINS` environment variable to the final GitHub Pages origin, for example `https://yourname.github.io/tutorplanner-booking`.
3. Configure Cloudflare Tunnel using `cloudflared-config.example.yml`. The ingress rule must match only `/api/public-booking/.*`; the final `404` rule is required.
4. Restart the local Tutorplanner app and the tunnel.
5. Confirm the API works from the Pages origin.

## GitHub Pages

Create a GitHub repository, copy this project into it, commit, and push to `main`. The included `.github/workflows/deploy-booking-pages.yml` publishes only `public-booking`. In GitHub, open Settings > Pages and set the source to GitHub Actions. The resulting URL can be used as the public origin.

GitHub Pages does not connect to SQLite directly. The local Tutorplanner application must be running, and a secure HTTPS tunnel must forward only `/api/public-booking/*` to `http://localhost:5167`.

Do not forward the whole local site. The existing Blazor application still contains private admin routes. The tunnel configuration must expose only the API path, and its catch-all must return 404. Test that the tunnel hostname root, `/students`, `/calendar`, and `/bookings` return 404. The local app remains available to you at `http://localhost:5167`.

## GitHub repository setup

From the repository root:

```powershell
git init
git add .
git commit -m "Add public Georgian booking page"
git branch -M main
git remote add origin https://github.com/YOUR-ACCOUNT/YOUR-REPOSITORY.git
git push -u origin main
```

Do not commit `tutorplanner.db`, tunnel credentials, or the local published app.
