// SVG coin icon — replaces 🪙 emoji which isn't supported on older systems
export default function CoinIcon({ size = 16 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 16 16"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: 2 }}
      aria-hidden="true"
    >
      <circle cx="8" cy="8" r="7.5" fill="#F5C518" stroke="#C9A000" strokeWidth="1" />
      <circle cx="8" cy="8" r="5.5" fill="#F5C518" stroke="#C9A000" strokeWidth="0.75" />
      <text
        x="8"
        y="11.5"
        textAnchor="middle"
        fontSize="7"
        fontWeight="bold"
        fill="#8B6914"
        fontFamily="serif"
      >$</text>
    </svg>
  )
}
