import { REPO_URL } from '../config'

const updated = '11 November 2026'

export default function LegalPage({ kind }) {
  const privacy = kind === 'privacy'
  const title = privacy ? 'Privacy Policy' : 'Terms and Conditions'

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

      <main className="legal wrap">
        <p className="eyebrow">Insominia / Legal</p>
        <h1>{title}</h1>
        <p className="legal__date">Last updated: {updated}</p>

        {privacy ? (
          <div className="legal__body">
            <p>This policy explains what happens to information when you visit the Insominia website or use the Windows app.</p>

            <h2>Website visits</h2>
            <p>The website has no account, contact form, or analytics code. It does not set its own tracking cookies. The service that hosts the website may receive ordinary request data such as your IP address, browser details, and pages requested to deliver the site and maintain security. Its retention practices depend on the host.</p>

            <h2>Desktop app data</h2>
            <p>Insominia saves settings and a diagnostic log on your computer in <code>%APPDATA%\Insominia</code>. Settings can include your selected apps, battery saver choice, timer, and saved lid behavior. Logs can include timestamps, app paths or names, status messages, and error details. The app does not include telemetry or automatically send these files to us.</p>

            <h2>Downloads and external services</h2>
            <p>The download flow links to the project on GitHub and then opens a Google Drive download URL. The website does not receive your GitHub account details or verify whether you starred the repository. It also loads fonts from Google Fonts. These services may receive your IP address and other request information under their own policies. See <a href="https://policies.google.com/privacy" target="_blank" rel="noreferrer">Google’s Privacy Policy</a> and <a href="https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement" target="_blank" rel="noreferrer">GitHub’s Privacy Statement</a>.</p>

            <h2>Your choices</h2>
            <p>You can stop using the app and remove its local settings and logs by closing it and deleting <code>%APPDATA%\Insominia</code>. The website does not provide a user account to delete. For data handled by Google, GitHub, or the website host, use those services’ privacy controls.</p>

            <h2>Changes and questions</h2>
            <p>We may update this page if the site or app changes. The updated date above will show when we last revised it. For questions about Insominia’s privacy practices, contact the maintainers through the <a href={REPO_URL} target="_blank" rel="noreferrer">project repository</a>.</p>
          </div>
        ) : (
          <div className="legal__body">
            <p>These terms apply to the Insominia website and Windows app. Please read them before using the app.</p>

            <h2>Using Insominia</h2>
            <p>Insominia is provided as a free utility for Windows 10 and 11. Use it only on a computer you are authorized to manage and for lawful purposes. The app changes the active Windows lid-close power setting while it runs and may ask for administrator approval during setup.</p>

            <h2>Power, heat, and your work</h2>
            <p>Keeping a closed laptop awake can use battery power and produce heat. Keep vents clear, follow your device maker’s guidance, and use the timer and safety settings that suit your device. Temperature sensors are unavailable on some computers, so the heat guard cannot always measure device temperature. Save or back up important work before relying on a long unattended task.</p>

            <h2>Availability and updates</h2>
            <p>We may change the website, app, download location, or these terms. We do not guarantee that the app will prevent every interruption, work with every Windows configuration, or remain available at all times.</p>

            <h2>External services</h2>
            <p>The download is delivered through Google Drive. Links to Google, GitHub, or other sites take you to services with their own terms and privacy practices.</p>

            <h2>Responsibility</h2>
            <p>The app is provided as available, without a promise of uninterrupted operation or fitness for a particular task. To the extent allowed by applicable law, the maintainers are not responsible for lost work, battery drain, overheating, or other losses arising from use of the app. This does not limit rights that cannot legally be excluded.</p>

            <h2>Questions</h2>
            <p>For questions about these terms, contact the maintainers through the <a href={REPO_URL} target="_blank" rel="noreferrer">project repository</a>.</p>
          </div>
        )}

        <div className="legal__end">
          <a href={privacy ? '#/terms' : '#/privacy'}>{privacy ? 'Read Terms and Conditions' : 'Read Privacy Policy'} →</a>
        </div>
      </main>

      <footer className="foot">
        <div className="wrap foot__in">
          <div className="foot__legal-brand">
            <span className="foot__maker">A NLR PRODUCT</span>
            <small>Made with love <span role="img" aria-label="blue heart">💙</span> for developers · Free to use.</small>
          </div>
          <div className="foot__links">
            <a href="#/pricing">Pricing</a>
            <a href="#/terms">Terms and Conditions</a>
            <a href="#/privacy">Privacy Policy</a>
          </div>
        </div>
      </footer>
    </>
  )
}
