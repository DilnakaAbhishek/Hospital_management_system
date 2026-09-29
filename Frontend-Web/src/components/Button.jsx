import './Button.css'

export default function Button({
  children,
  variant = 'primary',
  size = 'md',
  icon: Icon,
  iconRight,
  loading = false,
  disabled = false,
  fullWidth = false,
  onClick,
  type = 'button',
  id,
  className = '',
  ...rest
}) {
  return (
    <button
      id={id}
      type={type}
      className={[
        'btn',
        `btn--${variant}`,
        `btn--${size}`,
        fullWidth ? 'btn--full' : '',
        loading ? 'btn--loading' : '',
        className,
      ].filter(Boolean).join(' ')}
      disabled={disabled || loading}
      onClick={onClick}
      {...rest}
    >
      {loading && <span className="btn__spinner" aria-hidden="true" />}
      {!loading && Icon && <Icon size={15} strokeWidth={2} />}
      {children && <span>{children}</span>}
      {iconRight && !loading && <iconRight size={15} strokeWidth={2} />}
    </button>
  )
}
