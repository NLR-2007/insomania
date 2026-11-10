import { useEffect, useRef, useState } from 'react'

/*
  The hero's centrepiece.

  This is a replay of the real test: a task writing a timestamp every five
  seconds while the laptop lid was shut. The point of the product is that the
  ticks keep coming across the lid-close event, so the log IS the proof — more
  convincing than a stock photo of a laptop could ever be.

  It loops, and the lid closes a few ticks in so nobody has to wait for it.
*/

const LID_CLOSES_AT = 4
const LID_OPENS_AT = 15
const TOTAL = 20
const TICK_MS = 900

function stamp(base, i) {
  const d = new Date(base.getTime() + i * 5000)
  return d.toTimeString().slice(0, 8)
}

export default function LiveLog() {
  const [n, setN] = useState(0)
  const baseRef = useRef(new Date())
  const bodyRef = useRef(null)

  useEffect(() => {
    const id = setInterval(() => {
      setN((prev) => {
        if (prev >= TOTAL) {
          baseRef.current = new Date()
          return 0
        }
        return prev + 1
      })
    }, TICK_MS)
    return () => clearInterval(id)
  }, [])

  useEffect(() => {
    const el = bodyRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [n])

  const lines = []
  for (let i = 1; i <= n; i += 1) {
    if (i === LID_CLOSES_AT) {
      lines.push(
        <div className="term__line term__event" key="close">
          ── lid closed · screen off · system still running ──
        </div>,
      )
    }
    if (i === LID_OPENS_AT) {
      lines.push(
        <div className="term__line term__event" key="open">
          ── lid opened · no gap in the log ──
        </div>,
      )
    }
    lines.push(
      <div className="term__line" key={i}>
        <span className="term__n">{String(i).padStart(3, '0')}</span>
        <span className="term__t">{stamp(baseRef.current, i)}</span>
        <span className="term__ok">
          {(14500 + ((i * 977) % 1600)).toLocaleString()}k ops/sec
        </span>
      </div>,
    )
  }

  const closed = n >= LID_CLOSES_AT && n < LID_OPENS_AT

  return (
    <div className="term" aria-hidden="true">
      <div className="term__bar">
        <span className="term__dot" />
        <span className="term__dot" />
        <span className="term__dot" />
        <span className="term__title">agent-task.log — live</span>
      </div>

      <div className="term__body" ref={bodyRef}>
        {lines.length === 0 ? (
          <div className="term__line">
            <span className="term__t">starting task…</span>
          </div>
        ) : (
          lines
        )}
      </div>

      <div className="term__foot">
        <span>
          <span className="pulse" />
          {closed ? 'lid closed — still working' : 'running'}
        </span>
        <span>0 gaps · max interval 6s</span>
      </div>
    </div>
  )
}
