import { Fragment, useMemo } from "react";
import type { RotationStation } from "../../types/rotation";

const STATION_COLORS: Array<{ bg: string; text: string }> = [
  { bg: "#ffd966", text: "#5a3e00" },
  { bg: "#92d050", text: "#1a3a00" },
  { bg: "#00b0f0", text: "#003a52" },
  { bg: "#ff9900", text: "#5a2d00" },
  { bg: "#b4a7d6", text: "#2d1a5a" },
  { bg: "#87ceeb", text: "#1a3a52" },
  { bg: "#ff8c69", text: "#5a1a00" },
  { bg: "#98fb98", text: "#1a3a1a" },
];

const WEEKDAY_SHORT = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

const MONTH_NAMES = [
  "Januar", "Februar", "März", "April", "Mai", "Juni",
  "Juli", "August", "September", "Oktober", "November", "Dezember",
];

function abbreviateDept(name: string): string {
  const trimmed = name.trim();
  const words = trimmed.split(/\s+/);
  if (words.length === 1) return trimmed.slice(0, 5);
  return words.map(w => w[0] ?? "").join("").toUpperCase().slice(0, 5);
}

function toDateKey(year: number, month: number, day: number): string {
  return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function parseLocalDate(iso: string): Date {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(y, m - 1, d);
}

interface Props {
  stations: RotationStation[];
}

export default function RotationCalendarView({ stations }: Props) {
  const ordered = useMemo(
    () => [...stations].sort((a, b) => a.orderIndex - b.orderIndex),
    [stations]
  );

  const { months, dateToStation, colorByStationId, multiYear } = useMemo(() => {
    if (ordered.length === 0) return { months: [], dateToStation: new Map(), colorByStationId: new Map(), multiYear: false };

    const colorMap = new Map<number, number>();
    ordered.forEach((s, i) => colorMap.set(s.id, i % STATION_COLORS.length));

    const stationDateMap = new Map<string, RotationStation>();
    for (const station of ordered) {
      const start = parseLocalDate(station.startDate);
      const end = parseLocalDate(station.endDate);
      const cur = new Date(start);
      while (cur <= end) {
        const key = toDateKey(cur.getFullYear(), cur.getMonth(), cur.getDate());
        stationDateMap.set(key, station);
        cur.setDate(cur.getDate() + 1);
      }
    }

    const allStarts = ordered.map(s => parseLocalDate(s.startDate));
    const allEnds = ordered.map(s => parseLocalDate(s.endDate));
    const minDate = new Date(Math.min(...allStarts.map(d => d.getTime())));
    const maxDate = new Date(Math.max(...allEnds.map(d => d.getTime())));

    const monthList: Array<{ year: number; month: number }> = [];
    const cur = new Date(minDate.getFullYear(), minDate.getMonth(), 1);
    const endMonthTime = new Date(maxDate.getFullYear(), maxDate.getMonth(), 1).getTime();
    while (cur.getTime() <= endMonthTime && monthList.length < 24) {
      monthList.push({ year: cur.getFullYear(), month: cur.getMonth() });
      cur.setMonth(cur.getMonth() + 1);
    }

    const years = new Set(monthList.map(m => m.year));

    return {
      months: monthList,
      dateToStation: stationDateMap,
      colorByStationId: colorMap,
      multiYear: years.size > 1,
    };
  }, [ordered]);

  if (ordered.length === 0) return null;

  const border = "1px solid var(--border)";

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Kalenderansicht</h2>
        <p>Monatsübersicht des Abteilungsdurchlaufs – nur so weit wie der Plan reicht.</p>
      </div>

      <div style={{ overflowX: "auto", WebkitOverflowScrolling: "touch" as React.CSSProperties["WebkitOverflowScrolling"] }}>
        <table style={{ borderCollapse: "collapse", fontSize: "0.72rem", lineHeight: 1.3 }}>
          <thead>
            <tr>
              {months.map(({ year, month }) => (
                <th
                  key={`${year}-${month}`}
                  colSpan={3}
                  style={{
                    padding: "4px 6px",
                    textAlign: "center",
                    backgroundColor: "#1e3a5f",
                    color: "#ffffff",
                    fontWeight: 700,
                    border,
                    letterSpacing: "0.02em",
                    whiteSpace: "nowrap",
                  }}
                >
                  {MONTH_NAMES[month]}{multiYear ? ` ${year}` : ""}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: 31 }, (_, idx) => {
              const dayNum = idx + 1;
              return (
                <tr key={dayNum}>
                  {months.map(({ year, month }) => {
                    const daysInMonth = new Date(year, month + 1, 0).getDate();
                    if (dayNum > daysInMonth) {
                      return (
                        <td
                          key={`${year}-${month}-x`}
                          colSpan={3}
                          style={{ border, backgroundColor: "var(--bg-card-muted)" }}
                        />
                      );
                    }

                    const weekday = new Date(year, month, dayNum).getDay();
                    const isWeekend = weekday === 0 || weekday === 6;
                    const dateKey = toDateKey(year, month, dayNum);
                    const station = dateToStation.get(dateKey);
                    const colorIdx = station !== undefined ? (colorByStationId.get(station.id) ?? 0) : -1;
                    const color = colorIdx >= 0 ? STATION_COLORS[colorIdx] : null;
                    const cellBg = color?.bg ?? (isWeekend ? "var(--bg-card-muted)" : "var(--bg-card)");

                    return (
                      <Fragment key={`${year}-${month}`}>
                        <td
                          style={{
                            padding: "1px 3px",
                            border,
                            textAlign: "right",
                            backgroundColor: cellBg,
                            color: isWeekend ? "var(--text-secondary)" : "var(--text-primary)",
                            fontWeight: isWeekend ? 400 : 500,
                            minWidth: "16px",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {dayNum}
                        </td>
                        <td
                          style={{
                            padding: "1px 3px",
                            border,
                            backgroundColor: cellBg,
                            color: isWeekend ? "var(--text-secondary)" : "var(--text-tertiary)",
                            minWidth: "20px",
                          }}
                        >
                          {WEEKDAY_SHORT[weekday]}
                        </td>
                        <td
                          style={{
                            padding: "1px 5px",
                            border,
                            backgroundColor: cellBg,
                            color: color ? color.text : "var(--text-secondary)",
                            fontWeight: station ? 600 : 400,
                            minWidth: "32px",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {station ? abbreviateDept(station.departmentName) : ""}
                        </td>
                      </Fragment>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div style={{ marginTop: "12px", display: "flex", flexWrap: "wrap", gap: "10px" }}>
        {ordered.map((station, idx) => {
          const color = STATION_COLORS[idx % STATION_COLORS.length];
          return (
            <div
              key={station.id}
              style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "0.75rem" }}
            >
              <span
                style={{
                  display: "inline-block",
                  width: "14px",
                  height: "14px",
                  backgroundColor: color.bg,
                  border: "1px solid var(--border)",
                  borderRadius: "2px",
                  flexShrink: 0,
                }}
              />
              <span style={{ color: "var(--text-primary)" }}>
                {station.departmentName}
                <span style={{ color: "var(--text-secondary)" }}> ({abbreviateDept(station.departmentName)})</span>
              </span>
            </div>
          );
        })}
      </div>
    </section>
  );
}
