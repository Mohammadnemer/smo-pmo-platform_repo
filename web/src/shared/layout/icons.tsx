import type { SVGProps } from 'react';

type IconProps = SVGProps<SVGSVGElement>;

// Stroke-based, 16x16 icon set matching the shell's design prototype. Icons
// that encode reading/motion direction (chevrons, the workflow sequence, the
// breadcrumb separator) get `.icon-flip-rtl` from the caller so a single
// `:dir(rtl)` CSS rule mirrors them — see AppShell.css.

export function GridIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <rect x="2" y="2" width="5" height="5" rx="1" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <rect x="9" y="2" width="5" height="5" rx="1" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <rect x="2" y="9" width="5" height="5" rx="1" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <rect x="9" y="9" width="5" height="5" rx="1" fill="none" stroke="currentColor" strokeWidth="1.3" />
    </svg>
  );
}

export function TargetIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <circle cx="8" cy="8" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <circle cx="8" cy="8" r="2.4" fill="none" stroke="currentColor" strokeWidth="1.3" />
    </svg>
  );
}

export function FolderIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <rect x="2.5" y="3" width="11" height="10" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.4" />
      <line x1="2.5" y1="6.5" x2="13.5" y2="6.5" stroke="currentColor" strokeWidth="1.4" />
    </svg>
  );
}

export function WorkflowIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <circle cx="3.2" cy="8" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <circle cx="12.8" cy="8" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <line x1="4.8" y1="8" x2="11.2" y2="8" stroke="currentColor" strokeWidth="1.3" />
      <polyline points="9.4,5.8 11.6,8 9.4,10.2" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function TrendIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <rect x="2" y="9.5" width="2.8" height="4" rx="0.6" fill="currentColor" opacity="0.55" />
      <rect x="6.6" y="6.2" width="2.8" height="7.3" rx="0.6" fill="currentColor" opacity="0.75" />
      <rect x="11.2" y="2.6" width="2.8" height="10.9" rx="0.6" fill="currentColor" />
    </svg>
  );
}

export function InboxIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <path
        d="M2.5 9.2 4.1 3.6a1 1 0 0 1 1-.7h5.8a1 1 0 0 1 1 .7l1.6 5.6"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
      />
      <path
        d="M2.5 9.2h3.4l0.7 1.4h2.8l0.7-1.4h3.4v2.6a1 1 0 0 1-1 1H3.5a1 1 0 0 1-1-1z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function BellIcon(props: IconProps) {
  return (
    <svg width="17" height="17" viewBox="0 0 18 18" {...props}>
      <path
        d="M4.4 7.2a4.6 4.6 0 0 1 9.2 0v3l1.2 2.2H3.2L4.4 12.2z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.4"
        strokeLinejoin="round"
      />
      <path d="M7.3 14.6a1.8 1.8 0 0 0 3.4 0" fill="none" stroke="currentColor" strokeWidth="1.4" />
    </svg>
  );
}

export function SearchIcon(props: IconProps) {
  return (
    <svg width="15" height="15" viewBox="0 0 16 16" {...props}>
      <circle cx="6.6" cy="6.6" r="4.6" fill="none" stroke="currentColor" strokeWidth="1.4" />
      <line x1="10.1" y1="10.1" x2="14" y2="14" stroke="currentColor" strokeWidth="1.4" />
    </svg>
  );
}

export function ChevronIcon(props: IconProps) {
  return (
    <svg width="13" height="13" viewBox="0 0 12 12" {...props}>
      <polyline
        points="7.5,2 3.5,6 7.5,10"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function CaretDownIcon(props: IconProps) {
  return (
    <svg width="10" height="10" viewBox="0 0 10 10" {...props}>
      <polyline points="2,4 5,7 8,4" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </svg>
  );
}

export function LayersIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <polygon points="8,2.3 14,5.6 8,8.9 2,5.6" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinejoin="round" />
      <polyline points="2,8.4 8,11.7 14,8.4" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinejoin="round" />
      <polyline points="2,11.2 8,14.5 14,11.2" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinejoin="round" />
    </svg>
  );
}

export function ListIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <line x1="3" y1="4" x2="13" y2="4" stroke="currentColor" strokeWidth="1.3" />
      <line x1="3" y1="8" x2="13" y2="8" stroke="currentColor" strokeWidth="1.3" />
      <line x1="3" y1="12" x2="9" y2="12" stroke="currentColor" strokeWidth="1.3" />
    </svg>
  );
}

export function GaugeIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <path d="M2.5 11.5a5.5 5.5 0 0 1 11 0" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <line x1="8" y1="11.5" x2="10.2" y2="8.4" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <circle cx="8" cy="11.5" r="0.9" fill="currentColor" />
    </svg>
  );
}

export function MapIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <circle cx="3.4" cy="4" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="12.6" cy="4" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="8" cy="12" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.2" />
      <line x1="4.7" y1="5" x2="7.1" y2="10.6" stroke="currentColor" strokeWidth="1.1" />
      <line x1="11.3" y1="5" x2="8.9" y2="10.6" stroke="currentColor" strokeWidth="1.1" />
    </svg>
  );
}

export function FlagIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <line x1="3.5" y1="2.2" x2="3.5" y2="13.8" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <path
        d="M3.5 3.2c2-1 4 1 6 0s2 1 3 0.4V8c-1 0.6-1.8-0.6-3-0.4s-4 1-6 0z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function AlertTriangleIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <path
        d="M8 2.4 14.2 13H1.8z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
      />
      <line x1="8" y1="6.4" x2="8" y2="9.4" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <circle cx="8" cy="11.3" r="0.75" fill="currentColor" />
    </svg>
  );
}

export function PresentationIcon(props: IconProps) {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" {...props}>
      <rect x="2" y="2.5" width="12" height="8" rx="1" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <line x1="8" y1="10.5" x2="8" y2="13.2" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <line x1="5.2" y1="13.2" x2="10.8" y2="13.2" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
      <polyline
        points="4.2,8 6.6,5.2 8.6,6.8 11.8,3.6"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function SunIcon(props: IconProps) {
  return (
    <svg width="15" height="15" viewBox="0 0 16 16" {...props}>
      <circle cx="8" cy="8" r="3.2" fill="none" stroke="currentColor" strokeWidth="1.3" />
      <g stroke="currentColor" strokeWidth="1.3" strokeLinecap="round">
        <line x1="8" y1="0.8" x2="8" y2="2.4" />
        <line x1="8" y1="13.6" x2="8" y2="15.2" />
        <line x1="0.8" y1="8" x2="2.4" y2="8" />
        <line x1="13.6" y1="8" x2="15.2" y2="8" />
        <line x1="2.8" y1="2.8" x2="3.9" y2="3.9" />
        <line x1="12.1" y1="12.1" x2="13.2" y2="13.2" />
        <line x1="12.1" y1="3.9" x2="13.2" y2="2.8" />
        <line x1="2.8" y1="13.2" x2="3.9" y2="12.1" />
      </g>
    </svg>
  );
}

export function MoonIcon(props: IconProps) {
  return (
    <svg width="15" height="15" viewBox="0 0 16 16" {...props}>
      <path
        d="M13.8 9.9A5.6 5.6 0 0 1 6.1 2.2a5.6 5.6 0 1 0 7.7 7.7z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function BreadcrumbSepIcon(props: IconProps) {
  return (
    <svg width="9" height="9" viewBox="0 0 10 10" {...props}>
      <polyline
        points="3.5,2 6.5,5 3.5,8"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.4"
        strokeLinecap="round"
      />
    </svg>
  );
}
