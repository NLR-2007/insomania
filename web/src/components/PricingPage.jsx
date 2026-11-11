import { useState } from 'react'
import { DOWNLOAD_URL, RELEASE, REPO_URL } from '../config'

export default function PricingPage() {
  const [repoOpened, setRepoOpened] = useState(false)
  const [starConfirmed, setStarConfirmed] = useState(false)

  return (
    <>
      <header className="legal__nav">
        <div className="wrap legal__nav-inner">
          <a className="nav__brand" href="#top">
            <img src="./logo.png" alt="" width="26" height="26" />
            <b>Insominia</b>
          </a>
          <a className="legal__back" href="#top">← Back to website</a>
        </div>
      </header>

      <main className="pricing wrap">
        <div className="pricing__intro">
          <p className="eyebrow">Simple pricing</p>
          <h1>Keep working. Pay nothing.</h1>
          <p className="lede">Insominia is free and open source under the MIT License. <a className="pricing__source" href={REPO_URL} target="_blank" rel="noreferrer">Explore the code on GitHub</a>, star the project to support its development, then download the Windows app.</p>
        </div>

        <div className="pricing__card">
          <div className="pricing__offer">
            <span className="foot__maker">A NLR PRODUCT</span>
            <h2>Insominia</h2>
            <div className="pricing__amount"><span>$0</span><small>/ free to use</small></div>
            <p className="pricing__promise">Who are we to charge for your sleep?<br /><strong>Your sleep. Your freedom.</strong></p>
            <p className="pricing__version">Version {RELEASE.version} · Windows 10 / 11</p>
          </div>

          <div className="pricing__included">
            <h3>What is included</h3>
            <ul>
              <li>Keep Windows awake with the lid closed</li>
              <li>Choose which apps stay at full speed</li>
              <li>Battery, heat, and timer safeguards</li>
              <li>Local settings and readable diagnostic logs</li>
              <li>No Insominia account or subscription</li>
              <li>Open source under the MIT License</li>
            </ul>
          </div>
        </div>

        <section className="pricing__access" aria-labelledby="pricing-access-title">
          <div>
            <p className="eyebrow">Get the app</p>
            <h2 id="pricing-access-title">Star the project, then download.</h2>
            <p>A GitHub star helps other developers find Insominia. Open the repository, give it a star, and return here.</p>
          </div>
          <div className="pricing__actions">
            <a className="btn btn--ghost" href={REPO_URL} target="_blank" rel="noreferrer" onClick={() => setRepoOpened(true)}>1. Star on GitHub ↗</a>
            {!starConfirmed ? (
              <button className="btn btn--primary" type="button" disabled={!repoOpened} onClick={() => setStarConfirmed(true)}>2. I starred it</button>
            ) : DOWNLOAD_URL ? (
              <a className="btn btn--primary" href={DOWNLOAD_URL} download>Download Insominia for Windows</a>
            ) : (
              <p className="pricing__unavailable">The download is temporarily unavailable.</p>
            )}
            <small>This is an honor-based step; the site does not access your GitHub account or verify your star.</small>
          </div>
        </section>
      </main>

      <footer className="foot">
        <div className="wrap foot__in">
          <div className="foot__legal-brand">
            <span className="foot__maker">A NLR PRODUCT</span>
            <small>Made with love <span role="img" aria-label="blue heart">💙</span> for developers · Free to use.</small>
          </div>
          <div className="foot__links">
            <a href={REPO_URL} target="_blank" rel="noreferrer">GitHub</a>
            <a href="#/terms">Terms and Conditions</a>
            <a href="#/privacy">Privacy Policy</a>
          </div>
        </div>
      </footer>
    </>
  )
}
