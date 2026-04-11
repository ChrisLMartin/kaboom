import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { api, startGoogleLogin } from "./api";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [loading, setLoading] = useState(true);
  const [authState, setAuthState] = useState({
    isAuthenticated: false,
    user: null,
    externalProviders: []
  });

  async function refreshAuth() {
    setLoading(true);
    try {
      const payload = await api.getAuthStatus();
      setAuthState(payload);
    } catch {
      setAuthState({
        isAuthenticated: false,
        user: null,
        externalProviders: []
      });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refreshAuth();
  }, []);

  const value = useMemo(
    () => ({
      loading,
      isAuthenticated: authState.isAuthenticated,
      user: authState.user,
      externalProviders: authState.externalProviders,
      refreshAuth,
      async login(payload) {
        const nextState = await api.login(payload);
        setAuthState(nextState);
        return nextState;
      },
      async register(payload) {
        const nextState = await api.register(payload);
        setAuthState(nextState);
        return nextState;
      },
      async logout() {
        const nextState = await api.logout();
        setAuthState(nextState);
      },
      beginGoogleLogin(returnTo) {
        startGoogleLogin(returnTo);
      }
    }),
    [authState, loading]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }

  return context;
}
