import { useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth";

export default function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const [form, setForm] = useState({
    email: "",
    password: ""
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(searchParams.get("error") ?? "");

  const returnTo = searchParams.get("returnTo") ?? location.state?.returnTo ?? "/budget";

  async function handleSubmit(event) {
    event.preventDefault();
    setSubmitting(true);
    setError("");

    try {
      await auth.login(form);
      navigate(returnTo, { replace: true });
    } catch (nextError) {
      setError(nextError.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="auth-shell">
      <div className="auth-card">
        <p className="eyebrow">Kaboom</p>
        <h1>Sign in</h1>
        <p className="auth-copy">Access your budget from anywhere.</p>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            <span>Email</span>
            <input
              autoComplete="email"
              type="email"
              value={form.email}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
            />
          </label>
          <label>
            <span>Password</span>
            <input
              autoComplete="current-password"
              type="password"
              value={form.password}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
            />
          </label>

          {error ? <p className="auth-error">{error}</p> : null}

          <button className="auth-primary-button" disabled={submitting} type="submit">
            {submitting ? "Signing in..." : "Sign in"}
          </button>
        </form>

        {auth.externalProviders.includes("google") ? (
          <button
            className="auth-secondary-button"
            type="button"
            onClick={() => auth.beginGoogleLogin(returnTo)}
          >
            Continue with Google
          </button>
        ) : null}

        <p className="auth-switch">
          New here?{" "}
          <Link to={`/signup?returnTo=${encodeURIComponent(returnTo)}`}>
            Create an account
          </Link>
        </p>
      </div>
    </section>
  );
}
