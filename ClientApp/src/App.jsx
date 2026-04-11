import { NavLink, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "./auth";
import SidebarAccounts from "./components/SidebarAccounts";
import BudgetPage from "./pages/BudgetPage";
import LoginPage from "./pages/LoginPage";
import TransactionsPage from "./pages/TransactionsPage";
import ReportsPage from "./pages/ReportsPage";
import SignupPage from "./pages/SignupPage";

function navClassName({ isActive }) {
  return isActive ? "active" : "";
}

export default function App() {
  const auth = useAuth();
  const location = useLocation();

  if (auth.loading) {
    return (
      <section className="auth-shell">
        <div className="auth-card auth-card-loading">
          <p className="eyebrow">Kaboom</p>
          <h1>Loading…</h1>
        </div>
      </section>
    );
  }

  if (!auth.isAuthenticated) {
    const returnTo = `${location.pathname}${location.search}`;

    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/signup" element={<SignupPage />} />
        <Route path="*" element={<Navigate replace to={`/login?returnTo=${encodeURIComponent(returnTo)}`} />} />
      </Routes>
    );
  }

  return (
    <div className="shell">
      <aside className="sidebar">
        <NavLink className="brand" to="/budget">
          <span className="brand-mark">K</span>
          <span>
            <strong>Kaboom</strong>
          </span>
        </NavLink>

        <nav className="nav">
          <NavLink className={navClassName} to="/budget">
            Budget
          </NavLink>
          <NavLink className={navClassName} to="/transactions">
            Transactions
          </NavLink>
          <NavLink className={navClassName} to="/reports">
            Reports
          </NavLink>
        </nav>

        <SidebarAccounts />

        <div className="sidebar-user-panel">
          <div>
            <p className="eyebrow">Signed in</p>
            <strong>{auth.user?.displayName || auth.user?.email}</strong>
            <p className="sidebar-user-email">{auth.user?.email}</p>
          </div>
          <button type="button" className="sidebar-edit-toggle" onClick={() => auth.logout()}>
            Sign out
          </button>
        </div>
      </aside>

      <main className="main">
        <Routes>
          <Route path="/" element={<Navigate replace to="/budget" />} />
          <Route path="/budget" element={<BudgetPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/reports" element={<ReportsPage />} />
        </Routes>
      </main>
    </div>
  );
}
