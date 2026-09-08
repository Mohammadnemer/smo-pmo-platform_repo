import './Sparkline.css';

type SparklineTone = 'ok' | 'warn' | 'risk' | 'neutral';

interface SparklineProps {
  values: number[];
  tone?: SparklineTone;
  width?: number;
  height?: number;
  ariaLabel?: string;
}

/**
 * A trend line with no charting dependency — built from scratch like the Gantt (CLAUDE.md's
 * locked decisions), just far smaller in scope. Mirrors automatically in RTL (Sparkline.css)
 * so a KPI's oldest→newest reading order still reads start-to-end regardless of direction.
 */
export function Sparkline({ values, tone = 'neutral', width = 96, height = 28, ariaLabel }: SparklineProps) {
  if (values.length === 0) {
    return <span className="ui-sparkline ui-sparkline--empty" aria-hidden="true" />;
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1; // a flat series still draws a flat, centered line
  const stepX = values.length > 1 ? width / (values.length - 1) : 0;

  const points = values.map((value, index) => {
    const x = index * stepX;
    const y = height - ((value - min) / range) * height;
    return { x, y };
  });

  const last = points[points.length - 1];

  return (
    <svg
      className={`ui-sparkline ui-sparkline--${tone}`}
      width={width}
      height={height}
      viewBox={`0 0 ${width} ${height}`}
      role={ariaLabel ? 'img' : undefined}
      aria-label={ariaLabel}
      aria-hidden={ariaLabel ? undefined : true}
    >
      <polyline
        points={points.map((point) => `${point.x.toFixed(2)},${point.y.toFixed(2)}`).join(' ')}
        fill="none"
        strokeWidth={1.75}
      />
      <circle cx={last.x} cy={last.y} r={2} />
    </svg>
  );
}
