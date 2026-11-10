import { motion } from 'motion/react'

/*
  One shared scroll reveal, so every section enters the same way.
  Short travel, eased out, fires once — a page that keeps re-animating as you
  scroll up and down feels restless.
*/
export default function Reveal({ children, delay = 0, y = 18, className }) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '-60px' }}
      transition={{ duration: 0.6, delay, ease: [0.22, 0.8, 0.3, 1] }}
    >
      {children}
    </motion.div>
  )
}
