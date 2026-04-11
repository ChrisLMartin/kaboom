import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api } from "../api";

const filterKeys = ["date", "account", "category", "payee", "notes", "amount"];

function formatAmountInput(value) {
  return Number(value ?? 0).toFixed(2);
}

function createEmptyTransaction(accounts, selectedAccountIds = []) {
  const defaultAccountId = selectedAccountIds[0] && accounts.some((account) => account.id === selectedAccountIds[0])
    ? selectedAccountIds[0]
    : accounts[0]?.id ?? "";

  return {
    date: new Date().toISOString().slice(0, 10),
    accountId: defaultAccountId,
    categoryId: "",
    payee: "",
    notes: "",
    amount: "0.00"
  };
}

function toDraftMap(transactions) {
  return Object.fromEntries(
    transactions.map((transaction) => [
      transaction.id,
      {
        date: transaction.dateDisplay,
        accountId: transaction.accountId,
        categoryId: transaction.categoryId ?? "",
        payee: transaction.payee,
        notes: transaction.notes,
        amount: formatAmountInput(transaction.amount)
      }
    ])
  );
}

function readSelectedValues(searchParams, key) {
  const repeated = searchParams.getAll(key);
  if (repeated.length > 0) {
    return repeated;
  }

  if (key === "account" && searchParams.get("accountId")) {
    return [searchParams.get("accountId")];
  }

  return [];
}

function normalizeFilterValue(key, transaction) {
  switch (key) {
    case "date":
      return transaction.dateDisplay;
    case "account":
      return transaction.accountId;
    case "category":
      return transaction.categoryId ?? "__none__";
    case "payee":
      return transaction.payee || "__empty__";
    case "notes":
      return transaction.notes || "__empty__";
    case "amount":
      return formatAmountInput(transaction.amount);
    default:
      return "";
  }
}

function labelForFilterValue(key, value, data) {
  switch (key) {
    case "account":
      return data.accounts.find((account) => account.id === value)?.name ?? value;
    case "category":
      if (value === "__none__") {
        return "Ready to assign";
      }

      for (const group of data.categoryGroups) {
        const category = group.categories.find((item) => item.id === value);
        if (category) {
          return category.name;
        }
      }

      return value;
    case "payee":
    case "notes":
      return value === "__empty__" ? "Empty" : value;
    default:
      return value;
  }
}

export default function TransactionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [data, setData] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const selectedAccountIds = readSelectedValues(searchParams, "account");
  const [addForm, setAddForm] = useState(createEmptyTransaction([], selectedAccountIds));

  async function loadTransactions() {
    setLoading(true);
    setError("");

    try {
      const payload = await api.getTransactions();
      setData(payload);
      setDrafts(toDraftMap(payload.transactions));
      setAddForm((current) => {
        const hasTypedContent =
          current.payee.trim() || current.notes.trim() || current.categoryId || Number(current.amount || 0) !== 0;

        return hasTypedContent ? current : createEmptyTransaction(payload.accounts, selectedAccountIds);
      });
    } catch (nextError) {
      setError(nextError.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadTransactions();
  }, []);

  useEffect(() => {
    if (!data) {
      return;
    }

    setAddForm((current) => {
      const hasTypedContent =
        current.payee.trim() || current.notes.trim() || current.categoryId || Number(current.amount || 0) !== 0;

      if (hasTypedContent) {
        return current;
      }

      return {
        ...current,
        accountId: createEmptyTransaction(data.accounts, selectedAccountIds).accountId
      };
    });
  }, [data, selectedAccountIds.join("|")]);

  const categoryOptions = useMemo(() => data?.categoryGroups ?? [], [data]);
  const selectedFilters = useMemo(
    () =>
      Object.fromEntries(
        filterKeys.map((key) => [key, readSelectedValues(searchParams, key)])
      ),
    [searchParams]
  );

  const filteredTransactions = useMemo(() => {
    if (!data) {
      return [];
    }

    return data.transactions.filter((transaction) =>
      filterKeys.every((key) => {
        const selected = selectedFilters[key];
        if (!selected || selected.length === 0) {
          return true;
        }

        return selected.includes(normalizeFilterValue(key, transaction));
      })
    );
  }, [data, selectedFilters]);

  const filterOptions = useMemo(() => {
    if (!data) {
      return {};
    }

    return Object.fromEntries(
      filterKeys.map((key) => {
        const values = new Map();
        for (const transaction of data.transactions) {
          const value = normalizeFilterValue(key, transaction);
          if (!values.has(value)) {
            values.set(value, labelForFilterValue(key, value, data));
          }
        }

        return [
          key,
          [...values.entries()]
            .map(([value, label]) => ({ value, label }))
            .sort((left, right) => left.label.localeCompare(right.label))
        ];
      })
    );
  }, [data]);

  const activeFilterSummary = useMemo(() => {
    if (!data) {
      return [];
    }

    return filterKeys
      .map((key) => {
        const values = selectedFilters[key];
        if (!values || values.length === 0) {
          return null;
        }

        return {
          key,
          label: `${headingLabelForKey(key)}: ${values.map((value) => labelForFilterValue(key, value, data)).join(", ")}`
        };
      })
      .filter(Boolean);
  }, [data, selectedFilters]);

  function setFilterValues(key, values) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.delete(key);
    if (key === "account") {
      nextParams.delete("accountId");
    }

    values.forEach((value) => nextParams.append(key, value));
    setSearchParams(nextParams);
  }

  function toggleFilterValue(key, value) {
    const currentValues = new Set(selectedFilters[key]);
    if (currentValues.has(value)) {
      currentValues.delete(value);
    } else {
      currentValues.add(value);
    }

    setFilterValues(key, [...currentValues]);
  }

  function clearAllFilters() {
    const nextParams = new URLSearchParams(searchParams);
    filterKeys.forEach((key) => nextParams.delete(key));
    nextParams.delete("accountId");
    setSearchParams(nextParams);
  }

  function hasMeaningfulNewRowContent() {
    return Boolean(
      addForm.payee.trim() ||
        addForm.notes.trim() ||
        addForm.categoryId ||
        Number(addForm.amount || 0) !== 0
    );
  }

  async function commitAddTransaction() {
    if (!data) {
      return;
    }

    if (!hasMeaningfulNewRowContent()) {
      return;
    }

    if (!addForm.payee.trim() || !addForm.accountId) {
      return;
    }

    try {
      const payload = await api.addTransaction({
        date: addForm.date,
        accountId: addForm.accountId,
        categoryId: addForm.categoryId || null,
        payee: addForm.payee,
        notes: addForm.notes,
        amount: Number(addForm.amount || 0)
      });

      setData(payload);
      setDrafts(toDraftMap(payload.transactions));
      setAddForm(createEmptyTransaction(payload.accounts, selectedAccountIds));
      setStatus("Transaction saved.");
      setError("");
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  async function commitTransaction(transactionId) {
    const draft = drafts[transactionId];
    if (!draft) {
      return;
    }

    try {
      const payload = await api.updateTransaction(transactionId, {
        date: draft.date,
        accountId: draft.accountId,
        categoryId: draft.categoryId || null,
        payee: draft.payee,
        notes: draft.notes,
        amount: Number(draft.amount || 0)
      });

      setData(payload);
      setDrafts(toDraftMap(payload.transactions));
      setStatus("Transaction updated.");
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  if (loading) {
    return <section className="page-shell"><p className="empty-state">Loading transactions…</p></section>;
  }

  if (!data) {
    return <section className="page-shell"><p className="empty-state">Transactions unavailable.</p></section>;
  }

  return (
    <section className="page-shell">
      <div className="page-toolbar page-title-toolbar">
        <div className="page-title-group">
          <h1 className="page-title">Transactions</h1>
          {activeFilterSummary.length > 0
            ? activeFilterSummary.map((item) => (
                <span className="filter-pill" key={item.key}>
                  {item.label}
                </span>
              ))
            : <span className="filter-muted">All transactions</span>}
          {activeFilterSummary.length > 0 ? (
            <button type="button" className="secondary" onClick={clearAllFilters}>
              Clear filters
            </button>
          ) : null}
        </div>
        <div className="page-status">
          {status ? <span className="save-pill">{status}</span> : null}
          {error ? <span className="error-pill">{error}</span> : null}
        </div>
      </div>

      <section className="card register-card">
        <div className="register-table-wrap">
          <table className="register-table">
            <thead>
              <tr>
                <FilterHeader
                  label="Date"
                  options={filterOptions.date}
                  selectedValues={selectedFilters.date}
                  onToggle={(value) => toggleFilterValue("date", value)}
                />
                <FilterHeader
                  label="Account"
                  options={filterOptions.account}
                  selectedValues={selectedFilters.account}
                  onToggle={(value) => toggleFilterValue("account", value)}
                />
                <FilterHeader
                  label="Category"
                  options={filterOptions.category}
                  selectedValues={selectedFilters.category}
                  onToggle={(value) => toggleFilterValue("category", value)}
                />
                <FilterHeader
                  label="Payee"
                  options={filterOptions.payee}
                  selectedValues={selectedFilters.payee}
                  onToggle={(value) => toggleFilterValue("payee", value)}
                />
                <FilterHeader
                  label="Notes"
                  options={filterOptions.notes}
                  selectedValues={selectedFilters.notes}
                  onToggle={(value) => toggleFilterValue("notes", value)}
                />
                <FilterHeader
                  label="Amount"
                  options={filterOptions.amount}
                  selectedValues={selectedFilters.amount}
                  onToggle={(value) => toggleFilterValue("amount", value)}
                />
              </tr>
            </thead>
            <tbody>
              <tr className="register-new-row">
                <td>
                  <input
                    type="date"
                    value={addForm.date}
                    onChange={(event) => {
                      const nextForm = { ...addForm, date: event.target.value };
                      setAddForm(nextForm);
                    }}
                  />
                </td>
                <td>
                  <select
                    value={addForm.accountId}
                    onChange={(event) => {
                      const nextForm = { ...addForm, accountId: event.target.value };
                      setAddForm(nextForm);
                    }}
                  >
                    {data.accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <CategorySelect
                    groups={categoryOptions}
                    value={addForm.categoryId}
                    onChange={(value) => {
                      const nextForm = { ...addForm, categoryId: value };
                      setAddForm(nextForm);
                    }}
                  />
                </td>
                <td>
                  <input
                    type="text"
                    placeholder="New transaction"
                    value={addForm.payee}
                    onChange={(event) => {
                      setAddForm((current) => ({ ...current, payee: event.target.value }));
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        commitAddTransaction();
                      }
                    }}
                  />
                </td>
                <td>
                  <input
                    type="text"
                    placeholder="Notes"
                    value={addForm.notes}
                    onChange={(event) => {
                      setAddForm((current) => ({ ...current, notes: event.target.value }));
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        commitAddTransaction();
                      }
                    }}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    step="0.01"
                    placeholder="0.00"
                    value={addForm.amount}
                    onChange={(event) => {
                      setAddForm((current) => ({ ...current, amount: event.target.value }));
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        commitAddTransaction();
                      }
                    }}
                  />
                </td>
              </tr>
              {filteredTransactions.map((transaction) => {
                const draft = drafts[transaction.id];
                if (!draft) {
                  return null;
                }

                return (
                  <tr key={transaction.id}>
                    <td>
                      <input
                        type="date"
                        value={draft.date}
                        onChange={(event) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, date: event.target.value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") {
                            event.preventDefault();
                            commitTransaction(transaction.id);
                          }
                        }}
                      />
                    </td>
                    <td>
                      <select
                        value={draft.accountId}
                        onChange={(event) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, accountId: event.target.value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                      >
                        {data.accounts.map((account) => (
                          <option key={account.id} value={account.id}>
                            {account.name}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <CategorySelect
                        groups={categoryOptions}
                        value={draft.categoryId}
                        onChange={(value) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, categoryId: value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                      />
                    </td>
                    <td>
                      <input
                        type="text"
                        value={draft.payee}
                        onChange={(event) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, payee: event.target.value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") {
                            event.preventDefault();
                            commitTransaction(transaction.id);
                          }
                        }}
                      />
                    </td>
                    <td>
                      <input
                        type="text"
                        value={draft.notes}
                        onChange={(event) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, notes: event.target.value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") {
                            event.preventDefault();
                            commitTransaction(transaction.id);
                          }
                        }}
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        step="0.01"
                        value={draft.amount}
                        onChange={(event) =>
                          setDrafts((current) => ({
                            ...current,
                            [transaction.id]: { ...draft, amount: event.target.value }
                          }))
                        }
                        onBlur={() => commitTransaction(transaction.id)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") {
                            event.preventDefault();
                            commitTransaction(transaction.id);
                          }
                        }}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </section>
  );
}

function FilterHeader({ label, onToggle, options = [], selectedValues = [] }) {
  const hasSelection = selectedValues.length > 0;

  return (
    <th>
      <div className="register-header-cell">
        <span>{label}</span>
        <details className="header-filter">
          <summary className={hasSelection ? "active" : ""}>
            Filter{hasSelection ? ` (${selectedValues.length})` : ""}
          </summary>
          <div className="header-filter-menu">
            {options.length === 0 ? (
              <p className="filter-empty">No values</p>
            ) : (
              options.map((option) => (
                <label className="header-filter-option" key={option.value}>
                  <input
                    type="checkbox"
                    checked={selectedValues.includes(option.value)}
                    onChange={() => onToggle(option.value)}
                  />
                  <span>{option.label}</span>
                </label>
              ))
            )}
          </div>
        </details>
      </div>
    </th>
  );
}

function CategorySelect({ groups, value, onChange, onBlur }) {
  return (
    <select value={value} onChange={(event) => onChange(event.target.value)} onBlur={onBlur}>
      <option value="">Ready to assign</option>
      {groups.map((group) => (
        <optgroup key={group.id} label={group.name}>
          {group.categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  );
}

function headingLabelForKey(key) {
  switch (key) {
    case "date":
      return "Date";
    case "account":
      return "Account";
    case "category":
      return "Category";
    case "payee":
      return "Payee";
    case "notes":
      return "Notes";
    case "amount":
      return "Amount";
    default:
      return key;
  }
}
