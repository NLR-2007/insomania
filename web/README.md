# Insominia — website

Marketing site for the Insominia desktop app. React + Vite, no backend.

```bash
npm install
npm run dev        # http://localhost:5173
npm run build      # -> dist/
npm run preview    # serve the built site locally
```

---

## ① Set the download link (the only thing you must edit)

Everything about the release lives in [`src/config.js`](src/config.js).

1. Upload `Insominia.exe` to Google Drive.
2. Right-click it → **Share** → **General access** → **Anyone with the link**.
   Skip this and visitors hit a sign-in wall instead of a download.
3. Copy the share link and take the id out of the middle:

   ```
   https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUv/view?usp=sharing
                                  └──────── this part ────────┘
   ```

4. Paste it into `config.js`:

   ```js
   export const DRIVE_FILE_ID = '1AbCdEfGhIjKlMnOpQrStUv'
   ```

The site builds the direct-download URL from that id, so visitors get the file
rather than a Drive preview page they have to click through.

Until you set it, the download buttons are visibly disabled and the page shows
a note telling you what to do — better than a button that silently does nothing.

### A caveat about Drive and .exe files

Google cannot virus-scan a file this size silently, so Drive may show an
"can't scan this file for viruses" interstitial before downloading. That is
normal, but it does cost you some trust at the exact moment you are asking for
it. If this becomes a problem, **GitHub Releases** is a better home for a
Windows binary: direct links, no interstitial, and version history. The site
needs no changes — just put the release URL in `DOWNLOAD_URL` instead.

---

## ② Deploy

The build is fully static, and `vite.config.js` sets `base: './'` so it works
from a subfolder as well as a domain root.

| Host | What to do |
|---|---|
| **Netlify / Vercel** | Point at this folder. Build `npm run build`, publish `dist`. |
| **GitHub Pages** | `npm run build`, then publish `dist/` to the `gh-pages` branch. |
| **Any static host** | Upload the contents of `dist/`. |

---

## Structure

```
src/
  config.js              release details + the download link        ← edit this
  App.jsx                the whole page
  styles.css             the design system
  components/
    LiveLog.jsx          the hero terminal, replaying the real test
    Faq.jsx              accordion
    Reveal.jsx           shared scroll-reveal wrapper
public/
  logo.png               shared with the desktop app
```

## Design notes

Dark, editorial, deliberately not a SaaS landing page. Instrument Serif for
display, Schibsted Grotesk for body, JetBrains Mono for anything that is data.
One accent — `#20e8d2`, sampled from the app's own logo so the site and the
product are visibly the same object.

The hero shows a live terminal replaying the actual heartbeat test: a task
ticking every five seconds, straight through the lid closing, with no gap. The
proof is the hero image, because for this product the proof *is* the pitch.

**Every number on the page is measured, not marketing** — 0 gaps, 6-second
maximum interval, 79% less work by throttled apps. If the app changes, change
these too rather than letting them drift into claims.
