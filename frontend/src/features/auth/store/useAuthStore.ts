import { create } from "zustand";
import api from "@/lib/axios";

export interface AuthUser {
  id?: string;
  name: string;
  email: string;
  role?: string | number;
  userType: "customer" | "employee";
  expiresAt?: string;
}

interface AuthState {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  loginCustomer: (email: string, password: string) => Promise<{ success: boolean; error?: string }>;
  loginWithGoogle: (idToken: string) => Promise<{ success: boolean; role?: string; error?: string }>;
  loginEmployee: (email: string, password: string) => Promise<{ success: boolean; error?: string }>;
  registerCustomer: (data: {
    firstName: string;
    lastName: string;
    email: string;
    phone: string;
    password: string;
    nationalId?: string;
    nationality?: string;
  }) => Promise<{ success: boolean; customerId?: string; error?: string }>;
  logout: () => void;
  initialize: () => void;
  clearError: () => void;
}

const mapEmployeeRoleName = (role: number | string): string => {
  const roleMap: Record<number, string> = {
    0: "Admin",
    1: "Manager",
    2: "FrontDesk",
    3: "Housekeeping",
    4: "Maintenance",
    5: "Kitchen",
  };
  if (typeof role === "number" && roleMap[role]) {
    return roleMap[role];
  }
  return String(role);
};

const extractErrorMessage = (err: any, fallback: string): string => {
  if (!err) return fallback;
  if (err.response?.data) {
    const data = err.response.data;
    if (typeof data === "string") return data;
    if (data.message && typeof data.message === "string") return data.message;
    if (data.error && typeof data.error === "string") return data.error;
    if (data.title && typeof data.title === "string") {
      if (data.errors && typeof data.errors === "object") {
        const firstErrorKey = Object.keys(data.errors)[0];
        const firstErrorVal = data.errors[firstErrorKey];
        if (Array.isArray(firstErrorVal) && firstErrorVal.length > 0) {
          return `${firstErrorVal[0]}`;
        }
      }
      return data.title;
    }
    if (data.errors && typeof data.errors === "object") {
      const firstErrorKey = Object.keys(data.errors)[0];
      const firstErrorVal = data.errors[firstErrorKey];
      if (Array.isArray(firstErrorVal) && firstErrorVal.length > 0) {
        return `${firstErrorVal[0]}`;
      }
    }
  }
  if (err.message && typeof err.message === "string") {
    if (err.message.includes("Network Error") || err.code === "ERR_NETWORK" || err.code === "ECONNREFUSED") {
      return "Cannot connect to backend server. Please verify the backend API is running.";
    }
    return err.message;
  }
  return fallback;
};

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  token: null,
  isAuthenticated: false,
  isLoading: false,
  error: null,

  clearError: () => set({ error: null }),

  loginCustomer: async (email: string, password: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/v1/customers/login", {
        email: email.trim().toLowerCase(),
        password,
      });

      const { token, customerId, fullName, expiresAt } = response.data;

      const user: AuthUser = {
        id: customerId,
        name: fullName || "Valued Guest",
        email: email.trim().toLowerCase(),
        userType: "customer",
        role: "Guest",
        expiresAt,
      };

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", token);
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
      }

      set({
        token,
        user,
        isAuthenticated: true,
        isLoading: false,
        error: null,
      });
      return { success: true };
    } catch (err: any) {
      const errorMsg = extractErrorMessage(err, "Invalid email or password.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  loginWithGoogle: async (idToken: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/auth/google", { idToken });

      const data = response.data;
      const token = data.accessToken || data.token;
      const userObj = data.user;
      const userId = userObj?.id || data.customerId;
      const fullName = userObj?.name || data.fullName || (userObj ? `${userObj.firstName} ${userObj.lastName}`.trim() : "Valued User");
      const email = userObj?.email || "";
      const rawRole = userObj?.role || "Customer";
      const normalizedRole = mapEmployeeRoleName(rawRole);
      const isCustomer = normalizedRole.toLowerCase() === "customer" || normalizedRole.toLowerCase() === "guest";
      const userType: "customer" | "employee" = isCustomer ? "customer" : "employee";
      const expiresAt = data.expiresAt;

      const user: AuthUser = {
        id: userId,
        name: fullName,
        email,
        userType,
        role: normalizedRole,
        expiresAt,
      };

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", token);
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
        if (isCustomer) {
          localStorage.setItem("customer_token", token);
          localStorage.setItem("customer_user", JSON.stringify(data));
        }
      }

      set({
        token,
        user,
        isAuthenticated: true,
        isLoading: false,
        error: null,
      });
      return { success: true, role: normalizedRole };
    } catch (err: any) {
      const errorMsg = extractErrorMessage(err, "Your Google account is not registered in the SmartHotel system. Please contact an administrator.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  loginEmployee: async (email: string, password: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/v1/auth/employee/login", {
        email: email.trim().toLowerCase(),
        password,
      });

      const { token, employeeId, fullName, role, expiresAt } = response.data;

      const user: AuthUser = {
        id: employeeId,
        name: fullName || "Staff Member",
        email: email.trim().toLowerCase(),
        userType: "employee",
        role: mapEmployeeRoleName(role),
        expiresAt,
      };

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", token);
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
      }

      set({
        token,
        user,
        isAuthenticated: true,
        isLoading: false,
        error: null,
      });
      return { success: true };
    } catch (err: any) {
      const errorMsg = extractErrorMessage(err, "Invalid email or password.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  registerCustomer: async (data) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/v1/customers/register", {
        firstName: data.firstName.trim(),
        lastName: data.lastName.trim(),
        email: data.email.trim().toLowerCase(),
        phone: data.phone.trim(),
        password: data.password,
        nationalId: data.nationalId?.trim() || null,
        nationality: data.nationality?.trim() || null,
      });

      set({ isLoading: false, error: null });
      return { success: true, customerId: response.data?.customerId };
    } catch (err: any) {
      const errorMsg = extractErrorMessage(err, "Registration failed. Please check your details and try again.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  logout: () => {
    if (typeof window !== "undefined") {
      localStorage.removeItem("smarthotel_token");
      localStorage.removeItem("smarthotel_user");
    }
    set({ user: null, token: null, isAuthenticated: false, error: null });
  },

  initialize: () => {
    if (typeof window !== "undefined") {
      const token = localStorage.getItem("smarthotel_token");
      const userStr = localStorage.getItem("smarthotel_user");

      if (token && userStr) {
        try {
          const user: AuthUser = JSON.parse(userStr);

          // Check if token has expired
          if (user.expiresAt && new Date(user.expiresAt) <= new Date()) {
            localStorage.removeItem("smarthotel_token");
            localStorage.removeItem("smarthotel_user");
            set({ token: null, user: null, isAuthenticated: false });
            return;
          }

          set({ token, user, isAuthenticated: true });
        } catch {
          localStorage.removeItem("smarthotel_token");
          localStorage.removeItem("smarthotel_user");
          set({ token: null, user: null, isAuthenticated: false });
        }
      }
    }
  },
}));
