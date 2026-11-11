import { RELEASE } from '../config'

export default function PricingPage({ DownloadButton }) {
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
          <p className="lede">Insominia is free to use. Download the Windows app and keep your work moving when you close the lid.</p>
        </div>

        <div className="pricing__card">
          <div className="pricing__offer">
            <span className="foot__maker">A NLR PRODUCT</span>
            <h2>Insominia</h2>
            <div className="pricing__amount"><span>$0</span><small>/ free to use</small></div>
            <p className="pricing__promise">Who are we to charge for your sleep?<br /><strong>Your sleep. Your freedom.</strong></p>
            <DownloadButton large>Download for Windows</DownloadButton>
            <p className="pricing__version">Version {RELEASE.version} · Windows 10 / 11</p>
          </div>

          <div className="pricing__included">
            <h3>What is included</h3>
            <ul>
              <li>Keep Windows awake with the lid closed</li>
              <li>Choose which apps stay at full speed</li>
              <li>Battery, heat, and timer safeguards</li>
              <li>Local settings and readable diagnostic logs</li>
              <li>No account or subscription</li>
            </ul>
          </div>
        </div>
      </main>

      <footer className="foot">
        <div className="wrap foot__in">
          <div className="foot__legal-brand">
            <span className="foot__maker">A NLR PRODUCT</span>
            <small>Made with love <span role="img" aria-label="blue heart">💙</span> for developers · Free to use.</small>
          </div>
          <div className="foot__links">
            <a href="#/terms">Terms and Conditions</a>
            <a href="#/privacy">Privacy Policy</a>
          </div>
        </div>
      </footer>
    </>
  )
}
