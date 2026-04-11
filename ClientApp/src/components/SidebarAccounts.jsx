import { Link } from "react-router-dom";
import { useEffect, useState } from "react";
import { api, formatCurrency } from "../api";

const accountKinds = ["Checking", "Savings", "CreditCard", "Cash"];

function formatAmountInput(value) {
  return Number(value ?? 0).toFixed(2);
}

function orderForKind(kind) {
  switch (kind) {
    case "Checking":
      return 0;
    case "Savings":
      return 1;
    case "CreditCard":
      return 2;
    case "Cash":
      return 3;
    default:
      return 4;
  }
}

function labelForKind(kind) {
  switch (kind) {
    case "CreditCard":
      return "Credit Cards";
    default:
      return kind;
  }
}

export default function SidebarAccounts() {
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [isEditMode, setIsEditMode] = useState(false);
  const [addForm, setAddForm] = useState({
    name: "",
    kind: "Checking",
    balance: "0.00"
  });
  const [drafts, setDrafts] = useState({});

  async function loadAccounts() {
    setLoading(true);
    setError("");

    try {
      const nextAccounts = await api.getAccounts();
      setAccounts(nextAccounts);
      setDrafts(
        Object.fromEntries(
          nextAccounts.map((account) => [
            account.id,
            {
              name: account.name,
              kind: account.kind,
              balance: formatAmountInput(account.balance)
            }
          ])
        )
      );
    } catch (nextError) {
      setError(nextError.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadAccounts();
  }, []);

  async function handleAddAccount(event) {
    event.preventDefault();

    try {
      await api.addAccount({
        name: addForm.name,
        kind: addForm.kind,
        balance: Number(addForm.balance || 0)
      });

      setAddForm({
        name: "",
        kind: "Checking",
        balance: "0.00"
      });
      await loadAccounts();
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  async function handleUpdateAccount(accountId) {
    const draft = drafts[accountId];
    if (!draft) {
      return;
    }

    try {
      await api.updateAccount(accountId, {
        name: draft.name,
        kind: draft.kind,
        balance: Number(draft.balance || 0)
      });

      await loadAccounts();
    } catch (nextError) {
      setError(nextError.message);
    }
  }

  const groupedAccounts = [...accounts]
    .sort((left, right) => {
      const kindOrder = orderForKind(left.kind) - orderForKind(right.kind);
      return kindOrder !== 0 ? kindOrder : left.name.localeCompare(right.name);
    })
    .reduce((result, account) => {
      if (!result[account.kind]) {
        result[account.kind] = [];
      }

      result[account.kind].push(account);
      return result;
    }, {});

  const trackedTotal = accounts.reduce((sum, account) => sum + account.balance, 0);

  return (
    <section className="sidebar-accounts-panel">
      <div className="sidebar-accounts-head">
        <div>
          <p className="eyebrow">Accounts</p>
          <strong>All accounts</strong>
        </div>
        <div className="sidebar-accounts-actions">
          <button type="button" className="secondary sidebar-edit-toggle" onClick={() => setIsEditMode((current) => !current)}>
            {isEditMode ? "Done" : "Add / Edit"}
          </button>
          <span className={`sidebar-accounts-total ${trackedTotal >= 0 ? "positive" : "negative"}`}>
            {formatCurrency(trackedTotal)}
          </span>
        </div>
      </div>

      {loading ? <p className="sidebar-empty-state">Loading accounts…</p> : null}
      {error ? <p className="sidebar-error">{error}</p> : null}

      {!loading && !isEditMode ? (
        <div className="sidebar-account-groups">
          {Object.keys(groupedAccounts).length === 0 ? (
            <p className="sidebar-empty-state">No accounts yet.</p>
          ) : (
            Object.entries(groupedAccounts).map(([kind, entries]) => (
              <div className="sidebar-account-group" key={kind}>
                <p className="sidebar-account-kind">{labelForKind(kind)}</p>
                <div className="sidebar-account-list">
                  {entries.map((account) => {
                    const draft = drafts[account.id] ?? {
                      name: account.name,
                      kind: account.kind,
                      balance: formatAmountInput(account.balance)
                    };

                    return (
                      <div className="sidebar-account-item" key={account.id}>
                        <Link className="sidebar-account-link" to={`/transactions?accountId=${account.id}`}>
                          <span className="sidebar-account-name">{account.name}</span>
                          <span className={`sidebar-account-balance ${account.balance >= 0 ? "positive" : "negative"}`}>
                            {account.balanceFormatted}
                          </span>
                        </Link>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))
          )}
        </div>
      ) : null}

      {!loading && isEditMode ? (
        <div className="sidebar-edit-account-list">
          {accounts.map((account) => {
            const draft = drafts[account.id] ?? {
              name: account.name,
              kind: account.kind,
              balance: formatAmountInput(account.balance)
            };

            return (
              <form
                className="sidebar-account-edit-form"
                key={account.id}
                onSubmit={(event) => {
                  event.preventDefault();
                  handleUpdateAccount(account.id);
                }}
              >
                <div className="sidebar-account-edit-head">
                  <Link className="sidebar-account-edit-link" to={`/transactions?account=${account.id}`}>
                    {account.name}
                  </Link>
                  <span className={`sidebar-account-balance ${account.balance >= 0 ? "positive" : "negative"}`}>
                    {account.balanceFormatted}
                  </span>
                </div>
                <div className="sidebar-account-edit-grid">
                  <label>
                    <span>Name</span>
                    <input
                      type="text"
                      value={draft.name}
                      onChange={(event) =>
                        setDrafts((current) => ({
                          ...current,
                          [account.id]: { ...draft, name: event.target.value }
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>Type</span>
                    <select
                      value={draft.kind}
                      onChange={(event) =>
                        setDrafts((current) => ({
                          ...current,
                          [account.id]: { ...draft, kind: event.target.value }
                        }))
                      }
                    >
                      {accountKinds.map((kindOption) => (
                        <option key={kindOption} value={kindOption}>
                          {labelForKind(kindOption)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button type="submit" className="secondary">
                    Save
                  </button>
                </div>
              </form>
            );
          })}
          <form className="sidebar-account-edit-form sidebar-account-new-form" onSubmit={handleAddAccount}>
            <div className="sidebar-account-edit-head">
              <strong>New account</strong>
            </div>
            <div className="sidebar-account-edit-grid sidebar-account-edit-grid-new">
              <label>
                <span>Name</span>
                <input
                  type="text"
                  placeholder="Offset account"
                  value={addForm.name}
                  onChange={(event) => setAddForm((current) => ({ ...current, name: event.target.value }))}
                />
              </label>
              <label>
                <span>Type</span>
                <select
                  value={addForm.kind}
                  onChange={(event) => setAddForm((current) => ({ ...current, kind: event.target.value }))}
                >
                  {accountKinds.map((kindOption) => (
                    <option key={kindOption} value={kindOption}>
                      {labelForKind(kindOption)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Starting balance</span>
                <input
                  type="number"
                  step="0.01"
                  value={addForm.balance}
                  onChange={(event) => setAddForm((current) => ({ ...current, balance: event.target.value }))}
                />
              </label>
              <button type="submit">Create</button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  );
}
