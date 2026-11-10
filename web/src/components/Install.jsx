import Reveal from './Reveal'

/*
  An honest walkthrough.

  Insominia is not code-signed, so Google and Windows both warn about it. Those
  warnings look alarming and they are the most likely place for someone to give
  up. Hiding that would be dishonest and would not help anyone — showing the
  exact screens, and explaining why each appears, is what gets people through.
*/

const STEPS = [
  {
    n: '01',
    title: 'Google says it could not scan the file',
    body:
      'Google only virus-scans files under 100 MB, and Insominia is 139 MB because it carries its own runtime. It is not a warning about the file itself — Google simply did not look at it.',
    action: 'Click Download anyway.',
    img: './guide/1-drive-warning.png',
    alt: 'Google Drive page saying the file is too large to scan, with a Download anyway button',
    wide: true,
  },
  {
    n: '02',
    title: 'Windows blocks an app it has not seen before',
    body:
      'SmartScreen flags any program without a code-signing certificate, which costs a few hundred pounds a year. It is about reputation, not about anything found in the file.',
    action: 'Click More info.',
    img: './guide/2-smartscreen.png',
    alt: 'Windows SmartScreen dialog saying it prevented an unrecognised app from starting',
  },
  {
    n: '03',
    title: 'Then run it',
    body:
      'Once you expand More info, Windows shows the file name and offers to run it. Check it says Insominia.exe before you continue — that is worth doing for any download, not just this one.',
    action: 'Click Run anyway.',
    img: './guide/3-run-anyway.png',
    alt: 'The same dialog expanded, showing Insominia.exe and a Run anyway button',
  },
  {
    n: '04',
    title: 'It opens, then lives in your tray',
    body:
      'The panel opens straight away, and Insominia then lives beside your clock. Click the tray icon any time to bring it back. If you cannot see the icon, check the little chevron that hides overflow icons.',
    action: 'Click the tray icon any time to reopen it.',
    img: './guide/4-panel.png',
    alt: 'The Insominia panel showing the main switch, battery and auto-stop options',
    tall: true,
  },
  {
    n: '05',
    title: 'Finish setup — once',
    body:
      'Windows only lets an administrator change what closing the lid does, so Insominia asks for permission one time and registers a small helper. After this the switch works silently and the prompt never comes back.',
    action: 'Click Finish setup, then Yes.',
    img: './guide/5-setup.png',
    alt: 'The one-time setup dialog explaining why administrator permission is needed',
    wide: true,
  },
]

export default function Install() {
  return (
    <div className="steps">
      {STEPS.map((s, i) => (
        <Reveal key={s.n} delay={i * 0.04}>
          <div className={`step${s.tall ? ' step--tall' : ''}`}>
            <div className="step__text">
              <span className="step__n">{s.n}</span>
              <h3>{s.title}</h3>
              <p>{s.body}</p>
              <p className="step__do">{s.action}</p>
            </div>
            <figure className={`step__shot${s.wide ? ' step__shot--wide' : ''}`}>
              <img src={s.img} alt={s.alt} loading="lazy" />
            </figure>
          </div>
        </Reveal>
      ))}
    </div>
  )
}
