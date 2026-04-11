async function apiRequest(path, options = {}) {
  const response = await fetch(path, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers ?? {})
    },
    ...options
  });

  if (!response.ok) {
    let message = "Request failed.";

    try {
      const payload = await response.json();
      if (payload?.message) {
        message = payload.message;
      } else if (payload?.errors || payload?.request) {
        const values = Object.values(payload.errors ?? { request: payload.request }).flat();
        if (values.length > 0) {
          message = values.join(" ");
        }
      }
    } catch {
      message = response.statusText || message;
    }

    throw new Error(message);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

export const api = {
  getBudget(month, months = 3) {
    const query = new URLSearchParams({ month, months: String(months) });
    return apiRequest(`/api/budget?${query.toString()}`);
  },
  saveBudget(payload) {
    return apiRequest("/api/budget/allocations", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  },
  getTransactions() {
    return apiRequest("/api/transactions");
  },
  addTransaction(payload) {
    return apiRequest("/api/transactions", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  },
  updateTransaction(id, payload) {
    return apiRequest(`/api/transactions/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    });
  },
  getReports(month) {
    const query = new URLSearchParams({ month });
    return apiRequest(`/api/reports?${query.toString()}`);
  },
  getAccounts() {
    return apiRequest("/api/accounts");
  },
  addAccount(payload) {
    return apiRequest("/api/accounts", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  },
  updateAccount(id, payload) {
    return apiRequest(`/api/accounts/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    });
  },
  getCategories() {
    return apiRequest("/api/categories");
  },
  addCategory(payload) {
    return apiRequest("/api/categories", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  },
  renameCategory(id, name) {
    return apiRequest(`/api/categories/${id}`, {
      method: "PATCH",
      body: JSON.stringify({ name })
    });
  },
  renameCategoryGroup(id, name) {
    return apiRequest(`/api/category-groups/${id}`, {
      method: "PATCH",
      body: JSON.stringify({ name })
    });
  }
};

export function formatCurrency(value) {
  return new Intl.NumberFormat("en-AU", {
    style: "currency",
    currency: "AUD"
  }).format(value ?? 0);
}

export function formatMonthInput(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
}

export function addMonths(monthKey, amount) {
  const [year, month] = monthKey.split("-").map(Number);
  const date = new Date(year, month - 1 + amount, 1);
  return formatMonthInput(date);
}
