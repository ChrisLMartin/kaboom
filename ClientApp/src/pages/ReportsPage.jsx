import { useEffect, useState } from "react";
import { api, formatMonthInput } from "../api";

export default function ReportsPage() {
  const [month, setMonth] = useState(formatMonthInput(new Date()));
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadReports() {
      setLoading(true);
      setError("");

      try {
        const payload = await api.getReports(month);
        if (!ignore) {
          setData(payload);
        }
      } catch (nextError) {
        if (!ignore) {
          setError(nextError.message);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadReports();
    return () => {
      ignore = true;
    };
  }, [month]);

  if (loading) {
    return <section className="page-shell"><p className="empty-state">Loading reports…</p></section>;
  }

  if (!data) {
    return <section className="page-shell"><p className="empty-state">Reports unavailable.</p></section>;
  }

  return (
    <section className="page-shell">
      <div className="page-toolbar">
        <div className="month-switcher">
          <input type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
        </div>
        <div className="page-status">{error ? <span className="error-pill">{error}</span> : null}</div>
      </div>

      <div className="reports-grid">
        <article className="card">
          <div className="section-head">
            <strong>Spending by group</strong>
            <span>{data.monthLabel}</span>
          </div>
          <div className="report-list">
            {data.spendingByGroup.map((row) => (
              <div className="report-row" key={row.groupName}>
                <div>
                  <strong>{row.groupName}</strong>
                  <p>Remaining {row.remainingFormatted}</p>
                </div>
                <strong>{row.spentFormatted}</strong>
              </div>
            ))}
          </div>
        </article>

        <article className="card">
          <div className="section-head">
            <strong>Targets</strong>
            <span>{data.monthLabel}</span>
          </div>
          <div className="report-list">
            {data.targets.map((row) => (
              <div className="target-row" key={`${row.groupName}-${row.categoryName}`}>
                <div>
                  <strong>{row.categoryName}</strong>
                  <p>{row.groupName}</p>
                </div>
                <div className="target-meta">
                  <span>{row.availableFormatted} of {row.targetFormatted}</span>
                  <progress max="1" value={row.progressRatio} />
                </div>
              </div>
            ))}
          </div>
        </article>
      </div>
    </section>
  );
}
