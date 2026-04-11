import { NavLink, Navigate, Route, Routes } from "react-router-dom";
import SidebarAccounts from "./components/SidebarAccounts";
import BudgetPage from "./pages/BudgetPage";
import TransactionsPage from "./pages/TransactionsPage";
import ReportsPage from "./pages/ReportsPage";

function navClassName({ isActive }) {
  return isActive ? "active" : "";
}

export default function App() {
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
