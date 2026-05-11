import { useState, useEffect, useMemo } from "react";
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

const WEEKDAYS = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
const MONTH_NAMES = [
  "Januar", "Februar", "März", "April", "Mai", "Juni",
  "Juli", "August", "September", "Oktober", "November", "Dezember",
];

function toIso(year: number, month: number, day: number): string {
  return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function parseIsoToDate(iso: string): Date | null {
  if (!iso) return null;
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return null;
  return new Date(y, m - 1, d);
}

function getWeeksForMonth(year: number, month: number): Array<Array<number | null>> {
  const firstDayOffset = (new Date(year, month, 1).getDay() + 6) % 7; // Mon=0
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const cells: Array<number | null> = [
    ...Array<null>(firstDayOffset).fill(null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];
  while (cells.length % 7 !== 0) cells.push(null);
  const weeks: Array<Array<number | null>> = [];
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
  return weeks;
}

type Props = {
  startDate: string;
  endDate: string;
  onStartDateChange: (date: string) => void;
  onEndDateChange: (date: string) => void;
  stations: RotationStation[];
  editingStationId: number | null;
};

export default function StationDateRangePicker({
  startDate,
  endDate,
  onStartDateChange,
  onEndDateChange,
  stations,
  editingStationId,
}: Props) {
  const today = new Date();
  const initDate = parseIsoToDate(startDate) ?? today;

  const [viewYear, setViewYear] = useState(initDate.getFullYear());
  const [viewMonth, setViewMonth] = useState(initDate.getMonth());
  const [isPickingEnd, setIsPickingEnd] = useState(false);
  const [hoverDate, setHoverDate] = useState<string | null>(null);

  useEffect(() => {
    if (!startDate && !endDate) {
      setIsPickingEnd(false);
    }
  }, [startDate, endDate]);

  useEffect(() => {
    if (startDate) {
      const d = parseIsoToDate(startDate);
      if (d) {
        setViewYear(d.getFullYear());
        setViewMonth(d.getMonth());
      }
    }
  }, [startDate]);

  const bookedMap = useMemo(() => {
    const map = new Map<string, RotationStation>();
    for (const station of stations) {
      if (station.id === editingStationId) continue;
      const start = parseIsoToDate(station.startDate);
      const end = parseIsoToDate(station.endDate);
      if (!start || !end) continue;
      const cur = new Date(start);
      while (cur <= end) {
        map.set(toIso(cur.getFullYear(), cur.getMonth(), cur.getDate()), station);
        cur.setDate(cur.getDate() + 1);
      }
    }
    return map;
  }, [stations, editingStationId]);

  const colorByStationId = useMemo(() => {
    const sorted = [...stations].sort((a, b) => a.orderIndex - b.orderIndex);
    return new Map(sorted.map((s, i) => [s.id, i % STATION_COLORS.length]));
  }, [stations]);

  function prevMonth() {
    if (viewMonth === 0) { setViewYear(y => y - 1); setViewMonth(11); }
    else setViewMonth(m => m - 1);
  }

  function nextMonth() {
    if (viewMonth === 11) { setViewYear(y => y + 1); setViewMonth(0); }
    else setViewMonth(m => m + 1);
  }

  function handleDayClick(iso: string) {
    if (!isPickingEnd || !startDate) {
      onStartDateChange(iso);
      onEndDateChange("");
      setIsPickingEnd(true);
    } else if (iso >= startDate) {
      onEndDateChange(iso);
      setIsPickingEnd(false);
    } else {
      onStartDateChange(iso);
      onEndDateChange("");
    }
  }

  const weeks = useMemo(() => getWeeksForMonth(viewYear, viewMonth), [viewYear, viewMonth]);

  const activeEnd = isPickingEnd && !endDate && hoverDate ? hoverDate : endDate;
  const hasRange = !!(startDate && activeEnd && startDate !== activeEnd);
  const rangeLo = hasRange ? (startDate < activeEnd ? startDate : activeEnd) : startDate;
  const rangeHi = hasRange ? (startDate < activeEnd ? activeEnd : startDate) : startDate;

  const otherStations = useMemo(
    () => [...stations].sort((a, b) => a.orderIndex - b.orderIndex).filter(s => s.id !== editingStationId),
    [stations, editingStationId]
  );

  return (
    <div>
      <label className="field compact">
        <span>Startdatum</span>
        <input
          type="date"
          value={startDate}
          onChange={(e) => {
            onStartDateChange(e.target.value);
            setIsPickingEnd(!!e.target.value);
          }}
        />
      </label>
      <label className="field compact">
        <span>Enddatum</span>
        <input
          type="date"
          value={endDate}
          onChange={(e) => {
            onEndDateChange(e.target.value);
            if (e.target.value) setIsPickingEnd(false);
          }}
        />
      </label>

      <div
        style={{
          border: "1px solid var(--border)",
          borderRadius: "6px",
          overflow: "hidden",
          fontSize: "0.78rem",
          marginTop: "10px",
        }}
      >
        {/* Month navigation */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            backgroundColor: "#1e3a5f",
            color: "#fff",
            padding: "5px 4px",
          }}
        >
          <button
            type="button"
            onClick={prevMonth}
            aria-label="Vorheriger Monat"
            style={{
              background: "none",
              border: "none",
              color: "#fff",
              cursor: "pointer",
              padding: "2px 10px",
              fontSize: "1.1rem",
              lineHeight: 1,
            }}
          >
            ‹
          </button>
          <span style={{ flex: 1, textAlign: "center", fontWeight: 700, fontSize: "0.8rem" }}>
            {MONTH_NAMES[viewMonth]} {viewYear}
          </span>
          <button
            type="button"
            onClick={nextMonth}
            aria-label="Nächster Monat"
            style={{
              background: "none",
              border: "none",
              color: "#fff",
              cursor: "pointer",
              padding: "2px 10px",
              fontSize: "1.1rem",
              lineHeight: 1,
            }}
          >
            ›
          </button>
        </div>

        {/* Day grid */}
        <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
          <thead>
            <tr>
              {WEEKDAYS.map(wd => (
                <th
                  key={wd}
                  style={{
                    textAlign: "center",
                    padding: "5px 0 3px",
                    fontSize: "0.7rem",
                    color: "var(--text-secondary)",
                    fontWeight: 600,
                  }}
                >
                  {wd}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {weeks.map((week, wi) => (
              <tr key={wi}>
                {week.map((day, di) => {
                  if (day === null) return <td key={di} style={{ padding: "2px 1px" }} />;

                  const iso = toIso(viewYear, viewMonth, day);
                  const weekdayIdx = (new Date(viewYear, viewMonth, day).getDay() + 6) % 7;
                  const isWeekend = weekdayIdx >= 5;
                  const bookedStation = bookedMap.get(iso);

                  const isStart = iso === startDate;
                  const isEnd = iso === (endDate || (isPickingEnd && !endDate ? hoverDate : ""));
                  const isEndpoint = isStart || (isEnd && iso !== startDate);
                  const isInRange = hasRange && iso > rangeLo && iso < rangeHi;
                  const isRangeEdge = hasRange && (iso === rangeLo || iso === rangeHi);

                  let tdBg = "transparent";
                  if (isInRange || isRangeEdge) tdBg = "#d0e4f7";

                  let spanBg = "transparent";
                  let spanColor = isWeekend ? "var(--text-secondary)" : "var(--text-primary)";
                  let spanFw: number = isWeekend ? 400 : 500;
                  let title: string | undefined;

                  if (bookedStation && !isEndpoint && !isInRange && !isRangeEdge) {
                    const ci = colorByStationId.get(bookedStation.id) ?? 0;
                    tdBg = STATION_COLORS[ci].bg;
                    spanColor = STATION_COLORS[ci].text;
                    title = `Belegt: ${bookedStation.departmentName}`;
                  }

                  if (isStart || isRangeEdge) {
                    spanBg = "#1e3a5f";
                    spanColor = "#fff";
                    spanFw = 700;
                  } else if (isEnd && iso === startDate) {
                    // single point (start clicked, hovering same day)
                    spanBg = "#1e3a5f";
                    spanColor = "#fff";
                    spanFw = 700;
                  }

                  return (
                    <td
                      key={di}
                      onClick={() => handleDayClick(iso)}
                      onMouseEnter={() => setHoverDate(iso)}
                      onMouseLeave={() => setHoverDate(null)}
                      title={title}
                      style={{
                        padding: "2px 1px",
                        backgroundColor: tdBg,
                        cursor: "pointer",
                        userSelect: "none",
                      }}
                    >
                      <span
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          width: "30px",
                          height: "28px",
                          margin: "0 auto",
                          borderRadius: "50%",
                          backgroundColor: spanBg,
                          color: spanColor,
                          fontWeight: spanFw,
                          fontSize: "0.78rem",
                          transition: "background-color 0.1s",
                        }}
                      >
                        {day}
                      </span>
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {isPickingEnd && !endDate && startDate && (
        <p style={{ fontSize: "0.72rem", color: "var(--text-secondary)", margin: "5px 0 0" }}>
          Enddatum im Kalender auswählen…
        </p>
      )}

      {otherStations.length > 0 && (
        <div style={{ marginTop: "8px", display: "flex", flexWrap: "wrap", gap: "8px" }}>
          {otherStations.map(station => {
            const ci = colorByStationId.get(station.id) ?? 0;
            const c = STATION_COLORS[ci];
            return (
              <span
                key={station.id}
                style={{ display: "flex", alignItems: "center", gap: "5px", fontSize: "0.72rem", color: "var(--text-primary)" }}
              >
                <span
                  style={{
                    width: "10px",
                    height: "10px",
                    backgroundColor: c.bg,
                    border: "1px solid var(--border)",
                    borderRadius: "2px",
                    flexShrink: 0,
                    display: "inline-block",
                  }}
                />
                {station.departmentName}
              </span>
            );
          })}
        </div>
      )}
    </div>
  );
}
