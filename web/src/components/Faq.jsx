import { useState } from 'react'
import { AnimatePresence, motion } from 'motion/react'

const QUESTIONS = [
  {
    q: 'Why does it need administrator permission once?',
    a: [
      'Blocking idle sleep is something any program may do. Changing what the lid-close button does is a system power policy, and Windows only lets an administrator change that.',
      'Asking every time you flip the switch would mean a prompt on every on and every off, which is unusable. So you approve once, Insominia registers a small scheduled task that is allowed to run elevated, and from then on the switch works silently. You never see the prompt again.',
    ],
  },
  {
    q: 'Will it leave my laptop unable to sleep?',
    a: [
      'No. Your original lid setting is saved before anything is changed, and restored when you switch off, quit, log off — or if Insominia crashes.',
      'That last case is handled explicitly: if the app is killed while running, the next launch notices the lid is still overridden and puts your setting back without being asked. It is the one bug this app must not have.',
    ],
  },
  {
    q: 'Is it safe to run a laptop with the lid shut?',
    a: [
      'A closed laptop cannot breathe as well, so Insominia watches for it. It switches itself off above 90 °C, and below 15% battery when you are unplugged, rather than letting the machine cook or die mid-task.',
      'Some laptops do not expose a temperature sensor to Windows without administrator rights. When that happens the app says so plainly instead of pretending everything is fine — on those machines, run plugged in and set an auto-stop limit.',
    ],
  },
  {
    q: 'What does "battery saver" actually do to my apps?',
    a: [
      'It applies EcoQoS — the same mechanism Windows uses for background browser tabs. The app moves to efficiency cores and clocks down. It stays open, stays connected, and loses nothing.',
      'Nothing is closed and nothing is frozen, so there is no risk to unsaved work. Measured on real hardware, a throttled process did 79% less work, and returned to full speed the moment it was released.',
    ],
  },
  {
    q: 'How does it know not to slow down my agent?',
    a: [
      'You tick the apps that must keep running. An AI agent is never one process, though — it is a terminal plus node, python, git and whatever else it spawns.',
      'So ticking an app protects its whole process tree, including children started later. System processes, the shell, audio and security software are permanently off limits.',
    ],
  },
  {
    q: 'Does it work on my laptop?',
    a: [
      'Any Windows 10 or 11 laptop, 64-bit. There are no prerequisites — the app carries everything it needs, so there is no runtime to install first. Modern Standby machines — most recent laptops — often keep their Wi-Fi connection alive in standby, which suits this use case particularly well. You can check yours by running "powercfg /a" in a terminal.',
    ],
  },
  {
    q: 'How do I remove it?',
    a: [
      'Open Settings and press Remove next to the one-time setup, which deletes the helper task and restores your lid behaviour. Then delete the .exe. There is no installer and nothing in Program Files.',
    ],
  },
]

export default function Faq() {
  const [open, setOpen] = useState(0)

  return (
    <div className="faq">
      {QUESTIONS.map((item, i) => {
        const isOpen = open === i
        return (
          <div className="faq__item" data-open={isOpen} key={item.q}>
            <button
              className="faq__q"
              aria-expanded={isOpen}
              onClick={() => setOpen(isOpen ? -1 : i)}
            >
              {item.q}
            </button>

            <AnimatePresence initial={false}>
              {isOpen && (
                <motion.div
                  className="faq__a"
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={{ duration: 0.32, ease: [0.22, 0.8, 0.3, 1] }}
                >
                  {item.a.map((p) => (
                    <p key={p.slice(0, 24)}>{p}</p>
                  ))}
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )
      })}
    </div>
  )
}
