import { useEffect, useState } from 'react'
import { motion } from 'motion/react'
import LiveLog from './components/LiveLog'
import Reveal from './components/Reveal'
import Faq from './components/Faq'
import { DOWNLOAD_URL, RELEASE } from './config'

/* ── small pieces ─────────────────────────────────────────────────────── */

function DownloadButton({ large, children }) {
  const ready = Boolean(DOWNLOAD_URL)
  return (
    <a
      className={`btn btn--primary${large ? ' btn--lg' : ''}`}
      href={ready ? DOWNLOAD_URL : undefined}
      aria-disabled={!ready}
      title={ready ? 'Download Insominia for Windows' : 'Download link not configured yet'}
      onClick={(e) => { if (!ready) e.preventDefault() }}
      download
    >
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M12 3v12" /><path d="m7 11 5 5 5-5" /><path d="M4 21h16" />
      </svg>
      {children}
    </a>
  )
}

function Nav() {
  const [scrolled, setScrolled] = useState(false)
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  return (
    <nav className="nav" data-scrolled={scrolled}>
      <div className="wrap nav__inner">
        <a className="nav__brand" href="#top">
          <img src="./logo.png" alt="" width="26" height="26" />
          <b>Insominia</b>
        </a>
        <div className="nav__links">
          <a href="#problem">Why</a>
          <a href="#how">How it works</a>
          <a href="#proof">Proof</a>
          <a href="#faq">FAQ</a>
          <DownloadButton>Download</DownloadButton>
        </div>
      </div>
    </nav>
  )
}

/* ── page ─────────────────────────────────────────────────────────────── */

export default function App() {
  const fade = {
    initial: { opacity: 0, y: 24 },
    animate: { opacity: 1, y: 0 },
  }
  const ease = [0.22, 0.8, 0.3, 1]

  return (
    <>
      <Nav />

      {/* ══ HERO ══ */}
      <header className="hero" id="top">
        <div className="wrap hero__grid">
          <div>
            <motion.p className="eyebrow" {...fade} transition={{ duration: 0.5, ease }}>
              Windows 10 &amp; 11 · free
            </motion.p>

            <motion.h1 {...fade} transition={{ duration: 0.7, delay: 0.08, ease }}>
              Close the lid.
              <br />
              <span className="dim">Keep working.</span>
            </motion.h1>

            <motion.p className="hero__tag" {...fade} transition={{ duration: 0.7, delay: 0.18, ease }}>
              Give your laptop Insominia and get your freedom back.
            </motion.p>

            <motion.p className="lede hero__body" {...fade} transition={{ duration: 0.7, delay: 0.26, ease }}>
              Your AI agent needs another 25 minutes. Your train leaves in two.
              Shut the laptop and walk — Insominia keeps the machine working
              while the screen goes dark, so the task finishes without you.
            </motion.p>

            <motion.div className="hero__cta" {...fade} transition={{ duration: 0.7, delay: 0.34, ease }}>
              <DownloadButton large>Download for Windows</DownloadButton>
              <a className="btn btn--ghost btn--lg" href="#how">See how it works</a>
            </motion.div>

            <motion.p className="hero__meta" {...fade} transition={{ duration: 0.7, delay: 0.42, ease }}>
              v{RELEASE.version} · one file · nothing to install · no account
            </motion.p>
          </div>

          <motion.div
            initial={{ opacity: 0, y: 34, scale: 0.985 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.9, delay: 0.3, ease }}
          >
            <LiveLog />
          </motion.div>
        </div>
      </header>

      {/* ══ PROBLEM ══ */}
      <section className="sec" id="problem">
        <div className="wrap">
          <Reveal className="sec__head">
            <p className="eyebrow">The problem</p>
            <h2>
              The task was fine.
              <br />
              You just <em className="accent">moved</em>.
            </h2>
            <p className="lede">
              Agent tasks run for an hour. People do not sit still for an hour.
              The moment the lid shuts, Windows sleeps, the process freezes and
              the work is stranded — not because anything went wrong, but
              because you had a train to catch.
            </p>
          </Reveal>

          <Reveal delay={0.1}>
            <div className="story">
              <div className="story__step">
                <p className="story__clock">21:04</p>
                <h3>You start the task</h3>
                <p>An agent begins a refactor at the station. About an hour of work ahead of it.</p>
              </div>
              <div className="story__step">
                <p className="story__clock">21:35</p>
                <h3>The train arrives</h3>
                <p>Thirty-five minutes of progress done. Twenty-five still to go. You have to move.</p>
              </div>
              <div className="story__step story__step--bad">
                <p className="story__clock">21:36 · without Insominia</p>
                <h3>Everything stops</h3>
                <p>Lid shut, machine asleep, process frozen, network gone. The run is dead and has to start over.</p>
              </div>
              <div className="story__step story__step--good">
                <p className="story__clock">21:36 · with Insominia</p>
                <h3>Nothing stops</h3>
                <p>The screen goes off. The work carries on. You open the laptop later and it is finished.</p>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ══ HOW ══ */}
      <section className="sec" id="how">
        <div className="wrap">
          <Reveal className="sec__head">
            <p className="eyebrow">How it works</p>
            <h2>A lid is a screen, not a power switch.</h2>
            <p className="lede">
              Insominia does three things, and tells you honestly about all of
              them. There is no background service, no account, and nothing left
              behind that you cannot undo.
            </p>
          </Reveal>

          <Reveal delay={0.05}>
            <div className="feat">
              <div className="feat__no">01</div>
              <div className="feat__main">
                <h3>It tells Windows the machine is busy</h3>
                <div>
                  <p>
                    A standard power request stops Windows dozing off while work
                    is running. The screen is still allowed to switch off — only
                    the system has to stay up.
                  </p>
                  <p className="feat__note">No administrator rights needed.</p>
                </div>
              </div>
            </div>

            <div className="feat">
              <div className="feat__no">02</div>
              <div className="feat__main">
                <h3>It makes closing the lid do nothing</h3>
                <div>
                  <p>
                    Lid behaviour is a system power policy, so Windows asks for
                    permission once. After that a small helper applies it
                    silently and your original setting is always restored when
                    you switch off.
                  </p>
                  <p className="feat__note">One prompt, ever. Fully reversible.</p>
                </div>
              </div>
            </div>

            <div className="feat">
              <div className="feat__no">03</div>
              <div className="feat__main">
                <h3>It holds back everything you did not pick</h3>
                <div>
                  <p>
                    Your agent needs one app. Chrome, Slack and Teams do not need
                    to keep rendering inside a shut laptop. Tick what matters and
                    the rest is slowed right down until you switch off.
                  </p>
                  <p className="feat__note">EcoQoS — nothing closed, nothing frozen, nothing lost.</p>
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ══ PROOF ══ */}
      <section className="sec" id="proof">
        <div className="wrap">
          <Reveal className="sec__head">
            <p className="eyebrow">Measured, not claimed</p>
            <h2>Numbers from a real laptop.</h2>
            <p className="lede">
              A task wrote a timestamp every five seconds while the lid was shut.
              If Windows had slept, the log would show a hole exactly as long as
              the lid was closed.
            </p>
          </Reveal>

          <Reveal delay={0.08}>
            <div className="proof">
              <div className="stat">
                <div className="stat__n">0</div>
                <p className="stat__k">Gaps in the log</p>
                <p>73 consecutive ticks across the lid closing and opening again.</p>
              </div>
              <div className="stat">
                <div className="stat__n">6<small>sec</small></div>
                <p className="stat__k">Largest interval</p>
                <p>On a five-second heartbeat. No throttling while the lid was shut.</p>
              </div>
              <div className="stat">
                <div className="stat__n">79<small>%</small></div>
                <p className="stat__k">Less work by held-back apps</p>
                <p>Measured on a process doing continuous arithmetic, before and after throttling.</p>
              </div>
            </div>
          </Reveal>

          <Reveal delay={0.14}>
            <div className="bars">
              <div className="bar__row">
                <span className="bar__label">full speed</span>
                <div className="bar__track">
                  <motion.div
                    className="bar__fill"
                    style={{ background: 'linear-gradient(90deg,#20e8d2,#0e9c8c)' }}
                    initial={{ width: 0 }}
                    whileInView={{ width: '100%' }}
                    viewport={{ once: true }}
                    transition={{ duration: 1.1, ease }}
                  >
                    15,500k ops/sec
                  </motion.div>
                </div>
              </div>
              <div className="bar__row">
                <span className="bar__label">held back</span>
                <div className="bar__track">
                  <motion.div
                    className="bar__fill"
                    style={{ background: 'linear-gradient(90deg,#2a3537,#39474a)', color: '#9aa8ab' }}
                    initial={{ width: 0 }}
                    whileInView={{ width: '21%' }}
                    viewport={{ once: true }}
                    transition={{ duration: 1.1, delay: 0.18, ease }}
                  >
                    3,300k
                  </motion.div>
                </div>
              </div>
              <div className="bar__row">
                <span className="bar__label">released again</span>
                <div className="bar__track">
                  <motion.div
                    className="bar__fill"
                    style={{ background: 'linear-gradient(90deg,#20e8d2,#0e9c8c)' }}
                    initial={{ width: 0 }}
                    whileInView={{ width: '100%' }}
                    viewport={{ once: true }}
                    transition={{ duration: 1.1, delay: 0.36, ease }}
                  >
                    15,600k ops/sec
                  </motion.div>
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ══ SAFETY ══ */}
      <section className="sec">
        <div className="wrap">
          <Reveal className="sec__head">
            <p className="eyebrow">Safety</p>
            <h2>It knows when to stop.</h2>
            <p className="lede">
              A shut laptop running at load cannot breathe, and on battery it
              drains. Insominia is built to switch itself off rather than let
              either become your problem.
            </p>
          </Reveal>

          <Reveal delay={0.08}>
            <div className="safety">
              <div className="safety__item">
                <h4><span className="tick">▸</span> Battery guard</h4>
                <p>Below 15% and unplugged, it stops and tells you — instead of letting the machine die mid-task.</p>
              </div>
              <div className="safety__item">
                <h4><span className="tick">▸</span> Heat guard</h4>
                <p>Above 90 °C it switches off. If your laptop hides its sensor, the app says so rather than pretending.</p>
              </div>
              <div className="safety__item">
                <h4><span className="tick">▸</span> Auto-stop</h4>
                <p>Set 30 minutes to 4 hours. A forgotten session cannot quietly drain the battery overnight.</p>
              </div>
              <div className="safety__item">
                <h4><span className="tick">▸</span> Always restored</h4>
                <p>Your lid setting is saved before it is touched, and put back on exit, log-off — or after a crash.</p>
              </div>
              <div className="safety__item">
                <h4><span className="tick">▸</span> Hands off the system</h4>
                <p>The shell, audio, core Windows processes and security software are never slowed down.</p>
              </div>
              <div className="safety__item">
                <h4><span className="tick">▸</span> Nothing hidden</h4>
                <p>No installer, no service, no telemetry, no account. One file, and a log you can read.</p>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ══ DOWNLOAD ══ */}
      <section className="sec" id="download">
        <div className="wrap">
          <Reveal>
            <div className="dl">
              <h2>Stop waiting for your laptop.</h2>
              <p className="dl__tag">Give your laptop Insominia and get your freedom back.</p>

              <div className="dl__btns">
                <DownloadButton large>Download Insominia</DownloadButton>
                <a className="btn btn--ghost btn--lg" href="#faq">Read the FAQ first</a>
              </div>

              <div className="dl__specs">
                <span>version <b>{RELEASE.version}</b></span>
                <span>size <b>{RELEASE.size}</b></span>
                <span>released <b>{RELEASE.date}</b></span>
                <span>platform <b>Windows 10 / 11 · 64-bit</b></span>
              </div>

              <p className="dl__ready">
                <b>Nothing else to install.</b> Everything Insominia needs is
                inside the file — no runtime, no installer, no admin rights to
                download it. Save it, double-click it, and it runs.
              </p>

              <p className="dl__hint">
                Keep it somewhere permanent — <span className="mono">Documents</span> or
                a folder of your own — rather than leaving it in Downloads. The
                one-time setup remembers where the file lives, so moving it
                afterwards means approving that prompt again.
              </p>

              {RELEASE.runtime && (
                <p className="notice">
                  <b>Needs {RELEASE.runtime.name}.</b>{' '}
                  <a href={RELEASE.runtime.url} target="_blank" rel="noreferrer">
                    Install it here
                  </a>{' '}
                  once and the app will start.
                </p>
              )}

              {!DOWNLOAD_URL && (
                <p className="notice">
                  <b>Download link not set yet.</b> Put Insominia.exe in Google
                  Drive, share it with “Anyone with the link”, then paste the
                  file id into <code className="mono">src/config.js</code>.
                </p>
              )}
            </div>
          </Reveal>
        </div>
      </section>

      {/* ══ FAQ ══ */}
      <section className="sec" id="faq">
        <div className="wrap">
          <Reveal className="sec__head">
            <p className="eyebrow">Questions</p>
            <h2>The things worth asking.</h2>
          </Reveal>
          <Reveal delay={0.06}>
            <Faq />
          </Reveal>
        </div>
      </section>

      {/* ══ FOOTER ══ */}
      <footer className="foot">
        <div className="wrap foot__in">
          <div className="foot__brand">
            <img src="./logo.png" alt="" width="30" height="30" />
            <div>
              <b>Insominia</b>
              <small>Close the lid. Keep working.</small>
            </div>
          </div>
          <small>
            Made for people whose trains do not wait. · v{RELEASE.version}
          </small>
        </div>
      </footer>
    </>
  )
}
