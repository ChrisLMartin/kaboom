import { useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth";

export default function SignupPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const [form, setForm] = useState({
    displayName: "",
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
      await auth.register(form);
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
        <h1>Create account</h1>
        <p className="auth-copy">Start your own budget workspace and keep it synced online.</p>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            <span>Name</span>
            <input
              autoComplete="name"
              type="text"
              value={form.displayName}
              onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
            />
          </label>
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
              autoComplete="new-password"
              type="password"
              value={form.password}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
            />
          </label>

          {error ? <p className="auth-error">{error}</p> : null}

          <button className="auth-primary-button" disabled={submitting} type="submit">
            {submitting ? "Creating account..." : "Create account"}
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
          Already have an account?{" "}
          <Link to={`/login?returnTo=${encodeURIComponent(returnTo)}`}>
            Sign in
          </Link>
        </p>
      </div>
    </section>
  );
}
