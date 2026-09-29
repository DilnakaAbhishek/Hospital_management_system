import { useEffect, useRef } from 'react'
import { X } from 'lucide-react'
import './Modal.css'

export default function Modal({
  open,
  onClose,
  title,
  subtitle,
  children,
  size = 'md',
  id,
  closeOnOverlayClick = true,
  closeOnEscape = true,
}) {
  const overlayRef = useRef()

  useEffect(() => {
    if (!open) return
    const handleKey = (e) => {
      if (closeOnEscape && e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', handleKey)
      document.body.style.overflow = ''
    }
  }, [open, onClose, closeOnEscape])

  if (!open) return null

  return (
    <div
      className="modal-overlay"
      ref={overlayRef}
      onClick={(e) => {
        if (closeOnOverlayClick && e.target === overlayRef.current) {
          onClose()
        }
      }}
      role="dialog"
      aria-modal="true"
      aria-labelledby={id ? `${id}-title` : 'modal-title'}
    >
      <div className={`modal modal--${size}`}>
        <div className="modal__header">
          <div>
            <h2 className="modal__title" id={id ? `${id}-title` : 'modal-title'}>{title}</h2>
            {subtitle && <p className="modal__subtitle">{subtitle}</p>}
          </div>
          <button
            className="modal__close"
            onClick={onClose}
            aria-label="Close modal"
            id={id ? `${id}-close` : 'modal-close'}
          >
            <X size={18} />
          </button>
        </div>

        <div className="modal__body">
          {children}
        </div>
      </div>
    </div>
  )
}
