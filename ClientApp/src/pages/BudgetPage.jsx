import { useEffect, useMemo, useRef, useState } from "react";
import { addMonths, api, formatMonthInput } from "../api";

function buildDrafts(groups) {
  return Object.fromEntries(
    groups.flatMap((group) =>
      group.categories.flatMap((category) =>
        category.cells.map((cell) => [`${cell.monthKey}|${category.categoryId}`, cell.budgetedInput])
      )
    )
  );
}

export default function BudgetPage() {
  const [month, setMonth] = useState(formatMonthInput(new Date()));
  const [data, setData] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [collapsedGroups, setCollapsedGroups] = useState({});
  const [loading, setLoading] = useState(true);
  const [savingMessage, setSavingMessage] = useState("");
  const [error, setError] = useState("");
  const [modalOpen, setModalOpen] = useState(false);
  const [categoryForm, setCategoryForm] = useState({
    name: "",
    groupId: "",
    newGroupName: ""
  });

  const saveTimeout = useRef(null);

  async function loadBudget(nextMonth = month) {
    setLoading(true);
    setError("");

    try {
      const payload = await api.getBudget(nextMonth, 3);
      setData(payload);
      setDrafts(buildDrafts(payload.groups));
      setMonth(payload.startMonthKey);
    } catch (nextError) {
      setError(nextError.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadBudget(month);
  }, [month]);

  useEffect(() => {
    return () => {
      if (saveTimeout.current) {
        clearTimeout(saveTimeout.current);
      }
    };
  }, []);

  const groupOptions = useMemo(() => data?.groups ?? [], [data]);

  async function saveBudgetUpdate(update) {
    if (!data) {
      return;
    }

    if (saveTimeout.current) {
      clearTimeout(saveTimeout.current);
    }

    setSavingMessage("Saving…");
    setError("");

    try {
      const payload = await api.saveBudget({
        startMonthKey: data.startMonthKey,
        monthCount: data.months.length,
        updates: [update]
      });

      setData(payload);
      setDrafts(buildDrafts(payload.groups));
      setSavingMessage("Saved");
      window.setTimeout(() => setSavingMessage(""), 1200);
    } catch (nextError) {
      setError(nextError.message);
      setSavingMessage("");
    }
  }

  function scheduleBudgetSave(update) {
    if (saveTimeout.current) {
      clearTimeout(saveTimeout.current);
    }

    saveTimeout.current = window.setTimeout(() => {
      saveBudgetUpdate(update);
    }, 700);
  }

  async function handleRename(kind, id, name) {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }

    try {
      if (kind === "group") {
        await api.renameCategoryGroup(id, trimmed);
      } else {
        await api.renameCategory(id, trimmed);
      }

      await loadBudget();
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  async function handleAddCategory(event) {
    event.preventDefault();

    try {
      await api.addCategory({
        name: categoryForm.name,
        groupId: categoryForm.newGroupName ? null : categoryForm.groupId || null,
        newGroupName: categoryForm.newGroupName || null
      });

      setCategoryForm({
        name: "",
        groupId: "",
        newGroupName: ""
      });
      setModalOpen(false);
      await loadBudget();
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  if (loading) {
    return <section className="page-shell"><p className="empty-state">Loading budget…</p></section>;
  }

  if (!data) {
    return <section className="page-shell"><p className="empty-state">Budget unavailable.</p></section>;
  }

  return (
    <section className="page-shell">
      <div className="budget-table-wrap">
        <table className="budget-grid">
          <thead>
            <tr className="month-card-row">
              <th className="category-column month-card-spacer">
                <div className="month-card-spacer-content">
                  <label className="month-picker-field">
                    <span className="eyebrow">Month</span>
                    <input type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
                  </label>
                  <div className="page-status month-card-status">
                    {savingMessage ? <span className="save-pill">{savingMessage}</span> : null}
                    {error ? <span className="error-pill">{error}</span> : null}
                  </div>
                </div>
              </th>
              {data.months.map((summary, monthIndex) => (
                <th
                  className={`month-card-head ${monthClusterClass(monthIndex, data.months.length)}`}
                  colSpan="3"
                  key={summary.monthKey}
                >
                  {monthIndex === 0 ? (
                    <button
                      type="button"
                      className="month-nav-button month-nav-button-prev"
                      onClick={() => setMonth(addMonths(month, -1))}
                      aria-label="Previous month"
                    >
                      ‹
                    </button>
                  ) : null}
                  <article className="budget-month-card">
                    <p className="eyebrow">{summary.label}</p>
                    <div className="month-breakdown-list">
                      {summary.breakdown.map((item) => (
                        <div className="month-breakdown-row" key={item.label}>
                          <strong className={`month-breakdown-amount ${item.effect}`}>{item.amountFormatted}</strong>
                          <span>{item.label}</span>
                        </div>
                      ))}
                    </div>
                    <div className="available-panel">
                      <span>Available to budget</span>
                      <strong>{summary.availableToBudgetFormatted}</strong>
                    </div>
                  </article>
                  {monthIndex === data.months.length - 1 ? (
                    <button
                      type="button"
                      className="month-nav-button month-nav-button-next"
                      onClick={() => setMonth(addMonths(month, 1))}
                      aria-label="Next month"
                    >
                      ›
                    </button>
                  ) : null}
                </th>
              ))}
            </tr>
            <tr className="month-summary-row">
              <th className="category-column">
                <div className="category-head">
                  <span>Categories</span>
                  <button type="button" className="plus-button" onClick={() => setModalOpen(true)}>
                    +
                  </button>
                </div>
              </th>
              {data.months.map((summary, monthIndex) => (
                <MonthSummaryCells
                  key={summary.monthKey}
                  monthIndex={monthIndex}
                  summary={summary}
                  totalMonths={data.months.length}
                />
              ))}
            </tr>
          </thead>
          <tbody>
            {data.groups.map((group) => (
              <BudgetGroupBlock
                key={group.groupId}
                collapsed={Boolean(collapsedGroups[group.groupId])}
                drafts={drafts}
                group={group}
                onCommitBudgetUpdate={saveBudgetUpdate}
                onRename={handleRename}
                onScheduleBudgetSave={scheduleBudgetSave}
                setCollapsed={(nextValue) =>
                  setCollapsedGroups((current) => ({
                    ...current,
                    [group.groupId]: nextValue
                  }))
                }
                setDrafts={setDrafts}
              />
            ))}
          </tbody>
        </table>
      </div>

      {modalOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setModalOpen(false)}>
          <div className="modal-card" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <div className="modal-head">
              <strong>Add category</strong>
              <button type="button" className="ghost-button" onClick={() => setModalOpen(false)}>
                Close
              </button>
            </div>
            <form className="modal-form" onSubmit={handleAddCategory}>
              <label>
                <span>Category name</span>
                <input
                  type="text"
                  value={categoryForm.name}
                  onChange={(event) => setCategoryForm((current) => ({ ...current, name: event.target.value }))}
                />
              </label>
              <label>
                <span>Existing group</span>
                <select
                  value={categoryForm.groupId}
                  onChange={(event) =>
                    setCategoryForm((current) => ({ ...current, groupId: event.target.value, newGroupName: "" }))
                  }
                >
                  <option value="">Choose a group</option>
                  {groupOptions.map((group) => (
                    <option key={group.groupId} value={group.groupId}>
                      {group.groupName}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Or create a new group</span>
                <input
                  type="text"
                  value={categoryForm.newGroupName}
                  onChange={(event) =>
                    setCategoryForm((current) => ({ ...current, newGroupName: event.target.value, groupId: "" }))
                  }
                />
              </label>
              <button type="submit">Add category</button>
            </form>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function MonthSummaryCells({ monthIndex, summary, totalMonths }) {
  return (
    <>
      <th className={`month-summary-cell month-block-start ${monthEdgeClass(monthIndex, totalMonths, "start")}`}>
        <span className="month-subhead">Budgeted</span>
        <strong>{summary.budgetedFormatted}</strong>
      </th>
      <th className="month-summary-cell">
        <span className="month-subhead">Outflows</span>
        <strong>{summary.outflowsFormatted}</strong>
      </th>
      <th className={`month-summary-cell month-block-end ${monthEdgeClass(monthIndex, totalMonths, "end")}`}>
        <span className="month-subhead">Balance</span>
        <strong>{summary.balanceFormatted}</strong>
      </th>
    </>
  );
}

function GroupCells({ cell, monthIndex, totalMonths }) {
  return (
    <>
      <td className={`group-cell month-block-start ${monthEdgeClass(monthIndex, totalMonths, "start")}`}>
        {cell.budgetedFormatted}
      </td>
      <td className="group-cell">{cell.outflowsFormatted}</td>
      <td className={`group-cell month-block-end ${monthEdgeClass(monthIndex, totalMonths, "end")}`}>
        {cell.balanceFormatted}
      </td>
    </>
  );
}

function FragmentBudgetCells({ cell, monthIndex, onChange, onCommit, totalMonths, value }) {
  return (
    <>
      <td className={`budgeted-cell month-block-start ${monthEdgeClass(monthIndex, totalMonths, "start")}`}>
        <input
          className="budget-input"
          type="text"
          inputMode="decimal"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onBlur={onCommit}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              onCommit();
            }
          }}
        />
      </td>
      <td className="metric-cell">{cell.outflowsFormatted}</td>
      <td className={`metric-cell month-block-end ${monthEdgeClass(monthIndex, totalMonths, "end")}`}>
        {cell.balanceFormatted}
      </td>
    </>
  );
}

function BudgetGroupBlock({
  collapsed,
  drafts,
  group,
  onCommitBudgetUpdate,
  onRename,
  onScheduleBudgetSave,
  setCollapsed,
  setDrafts
}) {
  return (
    <>
      <tr className="group-row">
        <th className="category-column">
          <div className="group-head">
            <input
              className="inline-name-input group-name-input"
              type="text"
              defaultValue={group.groupName}
              onBlur={(event) => onRename("group", group.groupId, event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  event.currentTarget.blur();
                }
              }}
            />
            <button type="button" className="collapse-button" onClick={() => setCollapsed(!collapsed)}>
              {collapsed ? "▸" : "▾"}
            </button>
          </div>
        </th>
        {group.totals.map((cell, monthIndex) => (
          <GroupCells
            key={`${group.groupId}-${cell.monthKey}`}
            cell={cell}
            monthIndex={monthIndex}
            totalMonths={group.totals.length}
          />
        ))}
      </tr>
      {!collapsed
        ? group.categories.map((category) => (
            <tr className="category-row" key={category.categoryId}>
              <th className="category-column">
                <input
                  className="inline-name-input category-name-input"
                  type="text"
                  defaultValue={category.categoryName}
                  onBlur={(event) => onRename("category", category.categoryId, event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      event.currentTarget.blur();
                    }
                  }}
                />
              </th>
              {category.cells.map((cell, monthIndex) => {
                const draftKey = `${cell.monthKey}|${category.categoryId}`;
                return (
                  <FragmentBudgetCells
                    key={draftKey}
                    cell={cell}
                    monthIndex={monthIndex}
                    value={drafts[draftKey] ?? ""}
                    onChange={(nextValue) => {
                      setDrafts((current) => ({ ...current, [draftKey]: nextValue }));
                      onScheduleBudgetSave({
                        monthKey: cell.monthKey,
                        categoryId: category.categoryId,
                        value: nextValue
                      });
                    }}
                    onCommit={() =>
                      onCommitBudgetUpdate({
                        monthKey: cell.monthKey,
                        categoryId: category.categoryId,
                        value: drafts[draftKey] ?? ""
                      })
                    }
                    totalMonths={category.cells.length}
                  />
                );
              })}
            </tr>
          ))
        : null}
    </>
  );
}

function monthClusterClass(monthIndex, totalMonths) {
  const classes = [];
  if (monthIndex === 0) {
    classes.push("month-cluster-first");
  }

  if (monthIndex === totalMonths - 1) {
    classes.push("month-cluster-last");
  }

  return classes.join(" ");
}

function monthEdgeClass(monthIndex, totalMonths, edge) {
  if (edge === "start" && monthIndex === 0) {
    return "first-visible-month";
  }

  if (edge === "end" && monthIndex === totalMonths - 1) {
    return "last-visible-month";
  }

  return "";
}
