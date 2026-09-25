import api from "@/lib/axios";
import axios from "axios";
import { create } from "zustand";

export interface AuthUser {
  id?: string;
  name: string;
  email: string;
  role?: string | number;
  departmentId?: string;
  departmentName?: string;
  departmentCode?: string;
  designation?: string;
  userType: "customer" | "employee";
  expiresAt?: string;
  profileCompletionRequired?: boolean;
  profileCompletionDeadlineUtc?: string;
}

interface AuthState {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  mustResetPassword: boolean;
  isLoading: boolean;
  error: string | null;
  loginMember: (username: string, password: string) => Promise<{ success: boolean; error?: string }>;
  loginCustomer: (email: string, password: string) => Promise<{ success: boolean; error?: string }>;
  loginWithGoogle: (idToken: string) => Promise<{ success: boolean; role?: string; error?: string }>;
  loginEmployee: (email: string, password: string) => Promise<{ success: boolean; mustResetPassword?: boolean; error?: string }>;
  registerCustomer: (data: {
    firstName: string;
    lastName: string;
    email: string;
    phone: string;
    password: string;
    nationalId?: string;
    nationality?: string;
  }) => Promise<{ success: boolean; customerId?: string; message?: string; error?: string }>;
  verifyEmail: (email: string, token: string) => Promise<{ success: boolean; message?: string; error?: string }>;
  resendVerification: (email: string) => Promise<{ success: boolean; message?: string; error?: string }>;
  clearMustResetPassword: () => void;
  updateAfterPasswordReset: (newToken?: string, profileCompletionRequired?: boolean, deadline?: string, refreshToken?: string) => void;
  logout: () => void;
  initialize: () => Promise<void>;
  clearError: () => void;
}

const mapEmployeeRoleName = (role: number | string): string => {
  const roleMap: Record<number, string> = {
    0: "Owner",
    1: "Admin",
    2: "Manager",
    3: "FrontDesk",
    4: "Housekeeping",
    5: "Maintenance",
    6: "Kitchen",
    7: "Waiter",
    8: "Security",
  };
  if (typeof role === "number" && roleMap[role]) {
    return roleMap[role];
  }
  // Older identity records use "Receptionist", whereas the dashboard uses
  // "FrontDesk" as its canonical role name. Keep both login paths consistent.
  const roleName = String(role).trim();
  if (roleName.toLowerCase() === "receptionist") {
    return "FrontDesk";
  }
  if (roleName.toLowerCase() === "housekeeper") {
    return "Housekeeping";
  }
  if (roleName.toLowerCase() === "chef") {
    return "Kitchen";
  }

  return roleName;
};

const extractErrorMessage = (err: unknown, fallback: string): string => {
  if (!err) return fallback;
  if (axios.isAxiosError(err) && err.response?.data) {
    const data = err.response.data as Record<string, unknown>;
    if (typeof data === "string") return data;

    // 1. If errors array exists with items (e.g. ["Password is a commonly used insecure password..."])
    if (Array.isArray(data.errors) && data.errors.length > 0) {
      return data.errors.filter(Boolean).join(". ");
    }

    // 2. If errors is a validation dictionary (e.g. { "Email": ["Email is required"] })
    if (data.errors && typeof data.errors === "object" && !Array.isArray(data.errors)) {
      const errorList: string[] = [];
      for (const key of Object.keys(data.errors)) {
        const val = (data.errors as Record<string, unknown>)[key];
        if (Array.isArray(val)) {
          errorList.push(...val);
        } else if (typeof val === "string") {
          errorList.push(val);
        }
      }
      if (errorList.length > 0) {
        return errorList.join(". ");
      }
    }

    // 3. If explicit message exists
    if (data.message && typeof data.message === "string") return data.message;
    if (data.error && typeof data.error === "string") return data.error;
    if (data.title && typeof data.title === "string") return data.title;
  }
  if (err instanceof Error && err.message) {
    if (err.message.includes("Network Error") || (axios.isAxiosError(err) && ["ERR_NETWORK", "ECONNREFUSED"].includes(err.code ?? ""))) {
      return "Cannot connect to backend server. Please verify the backend API is running.";
    }
    return err.message;
  }
  return fallback;
};

const isJwt = (token: unknown): token is string =>
  typeof token === "string" && token.split(".").length === 3;

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  token: null,
  isAuthenticated: false,
  mustResetPassword: false,
  isLoading: false,
  error: null,

  clearError: () => set({ error: null }),

  loginMember: async (username: string, password: string) => {
    set({ isLoading: true, error: null });
    try {
      // Call POST /api/identity/login as required
      const response = await api.post("/api/identity/login", {
        username: username.trim(),
        password,
      });

      const { accessToken, refreshToken, member, expiresIn } = response.data;
      const user: AuthUser = {
        id: member?.id,
        name: member?.displayName || "Member",
        email: username.includes("@") ? username.trim().toLowerCase() : `${username.trim()}@smarthotel.internal`,
        userType: "customer",
        role: "Member",
        expiresAt: new Date(Date.now() + (expiresIn || 3600) * 1000).toISOString(),
      };

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", accessToken);
        if (refreshToken) {
          localStorage.setItem("smarthotel_refresh_token", refreshToken);
        }
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
      }

      set({
        token: accessToken,
        user,
        isAuthenticated: true,
        isLoading: false,
        error: null,
      });
      return { success: true };
    } catch (err: unknown) {
      const errorMsg = extractErrorMessage(err, "invalid_credentials");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  loginCustomer: async (email: string, password: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/v1/customers/login", {
        email: email.trim().toLowerCase(),
        password,
      });

      const data = response.data;
      // Backend returns: { accessToken, token, user: { id, name, email, role }, expiresIn, expiresAt }
      const token = data.accessToken || data.token;
      const userObj = data.user;
      const userId = userObj?.id || data.customerId;
      const fullName = userObj?.name || data.fullName
        || (userObj ? `${userObj.firstName} ${userObj.lastName}`.trim() : "Valued Guest");
      const userEmail = userObj?.email || email.trim().toLowerCase();
      const role = userObj?.role || "Guest";
      const expiresAt = data.expiresAt || new Date(Date.now() + (data.expiresIn || 3600) * 1000).toISOString();

      const user: AuthUser = {
        id: userId,
        name: fullName,
        email: userEmail,
        userType: "customer",
        role,
        expiresAt,
      };

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", token);
        if (data.refreshToken) localStorage.setItem("smarthotel_refresh_token", data.refreshToken);
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
        // Also store as customer_token for portal pages
        localStorage.setItem("customer_token", token);
        localStorage.setItem("customer_user", JSON.stringify(data));
      }

      set({
        token,
        user,
        isAuthenticated: true,
        isLoading: false,
        error: null,
      });
      return { success: true };
    } catch (err: unknown) {
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
        if (data.refreshToken) localStorage.setItem("smarthotel_refresh_token", data.refreshToken);
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
    } catch (err: unknown) {
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

      const data = response.data || {};
      const token = data.token || data.accessToken || data.jwtToken;
      const employeeId = data.employeeId || data.user?.id || "emp-user";
      const fullName =
        data.fullName ||
        data.user?.name ||
        (data.user?.firstName ? `${data.user.firstName} ${data.user.lastName}`.trim() : "") ||
        "Staff Member";
      const rawRole = data.role !== undefined ? data.role : (data.user?.role ?? "Staff");
      const expiresAt = data.expiresAt || new Date(Date.now() + 3600 * 1000).toISOString();

      const user: AuthUser = {
        id: employeeId,
        name: fullName,
        email: email.trim().toLowerCase(),
        userType: "employee",
        role: mapEmployeeRoleName(rawRole),
        departmentId: data.departmentId || data.user?.departmentId,
        departmentName: data.departmentName || data.user?.departmentName,
        departmentCode: data.departmentCode || data.user?.departmentCode,
        designation: data.designation || data.user?.designation,
        expiresAt,
        profileCompletionRequired: Boolean(data.profileCompletionRequired),
        profileCompletionDeadlineUtc: data.profileCompletionDeadlineUtc,
      };

      const mustChangePassword = Boolean(data.mustChangePassword);

      if (!isJwt(token)) {
        throw new Error("The identity service did not return a valid access token.");
      }

      if (typeof window !== "undefined") {
        localStorage.setItem("smarthotel_token", token);
        if (data.refreshToken) localStorage.setItem("smarthotel_refresh_token", data.refreshToken);
        localStorage.setItem("smarthotel_user", JSON.stringify(user));
        if (mustChangePassword) {
          localStorage.setItem("smarthotel_must_reset_password", "true");
        } else {
          localStorage.removeItem("smarthotel_must_reset_password");
        }
      }

      set({
        token,
        user,
        isAuthenticated: true,
        mustResetPassword: mustChangePassword,
        isLoading: false,
        error: null,
      });
      return { success: true, mustResetPassword: mustChangePassword };
    } catch (err: unknown) {
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

      const message = response.data?.message;
      set({ isLoading: false, error: null });
      return { success: true, customerId: response.data?.customerId, message };
    } catch (err: unknown) {
      const errorMsg = extractErrorMessage(err, "Registration failed. Please check your details and try again.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  verifyEmail: async (email: string, token: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.get("/api/v1/auth/verify-email", {
        params: {
          email: email.trim().toLowerCase(),
          token: token.trim(),
        },
      });

      const message = response.data?.message || "Email verified successfully.";
      set({ isLoading: false, error: null });
      return { success: true, message };
    } catch (err: unknown) {
      const errorMsg = extractErrorMessage(err, "Verification failed or token has expired.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  resendVerification: async (email: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await api.post("/api/v1/auth/resend-verification", {
        email: email.trim().toLowerCase(),
      });

      const message = response.data?.message || "Verification email sent.";
      set({ isLoading: false, error: null });
      return { success: true, message };
    } catch (err: unknown) {
      const errorMsg = extractErrorMessage(err, "Could not resend verification email. Please try again later.");
      set({ error: errorMsg, isLoading: false });
      return { success: false, error: errorMsg };
    }
  },

  clearMustResetPassword: () => {
    if (typeof window !== "undefined") {
      localStorage.removeItem("smarthotel_must_reset_password");
    }
    set({ mustResetPassword: false });
  },

  updateAfterPasswordReset: (newToken?: string, profileCompletionRequired = false, deadline?: string, refreshToken?: string) => {
    if (typeof window !== "undefined") {
      localStorage.removeItem("smarthotel_must_reset_password");
      if (newToken) {
        localStorage.setItem("smarthotel_token", newToken);
      }
      if (refreshToken) localStorage.setItem("smarthotel_refresh_token", refreshToken);
    }
    set((state) => {
      const user = state.user ? { ...state.user, profileCompletionRequired, profileCompletionDeadlineUtc: deadline } : null;
      if (typeof window !== "undefined" && user) localStorage.setItem("smarthotel_user", JSON.stringify(user));
      return { mustResetPassword: false, token: newToken || state.token, user };
    });
  },

  logout: () => {
    if (typeof window !== "undefined") {
      const refreshToken = localStorage.getItem("smarthotel_refresh_token");
      if (refreshToken) {
        void api.post("/api/v1/auth/logout", { refreshToken }).catch(() => undefined);
      }
      localStorage.removeItem("smarthotel_token");
      localStorage.removeItem("smarthotel_refresh_token");
      localStorage.removeItem("smarthotel_user");
      localStorage.removeItem("customer_token");
      localStorage.removeItem("customer_user");
      localStorage.removeItem("smarthotel_must_reset_password");
    }
    set({ user: null, token: null, isAuthenticated: false, mustResetPassword: false, error: null });
  },

  initialize: async () => {
    if (typeof window !== "undefined") {
      const token = localStorage.getItem("smarthotel_token");
      const userStr = localStorage.getItem("smarthotel_user");
      const mustReset = localStorage.getItem("smarthotel_must_reset_password") === "true";

      if (token && userStr) {
        try {
          if (!isJwt(token)) {
            throw new Error("Stored access token is invalid.");
          }
          const user: AuthUser = JSON.parse(userStr);

          // A temporary employee token is deliberately restricted by the
          // Identity service to /api/v1/auth/change-password. Calling /auth/me
          // with it correctly returns 403, so trust only the local routing
          // marker long enough to send the user to the forced-reset screen.
          if (mustReset) {
            set({ token, user, isAuthenticated: true, mustResetPassword: true });
            return;
          }

          // Check if token has expired
          if (user.expiresAt && new Date(user.expiresAt) <= new Date()) {
            const refreshToken = localStorage.getItem("smarthotel_refresh_token");
            if (refreshToken) {
              try {
                const response = await api.post("/api/v1/auth/refresh", { refreshToken });
                const refreshedToken = response.data.accessToken;
                localStorage.setItem("smarthotel_token", refreshedToken);
                localStorage.setItem("smarthotel_refresh_token", response.data.refreshToken);
                user.expiresAt = new Date(Date.now() + (response.data.expiresIn || 3600) * 1000).toISOString();
                localStorage.setItem("smarthotel_user", JSON.stringify(user));
                set({ token: refreshedToken, user, isAuthenticated: true, mustResetPassword: mustReset });
                return;
              } catch {
                // Fall through and clear the expired session.
              }
            }
            localStorage.removeItem("smarthotel_token");
            localStorage.removeItem("smarthotel_refresh_token");
            localStorage.removeItem("smarthotel_user");
            localStorage.removeItem("smarthotel_must_reset_password");
            set({ token: null, user: null, isAuthenticated: false, mustResetPassword: false });
            return;
          }

          // A JWT-shaped value is not necessarily a valid session. Validate it
          // through the gateway before protected layouts trust persisted state.
          // The Axios interceptor will attempt one refresh when appropriate.
          await api.get("/api/v1/auth/me");
          const validatedToken = localStorage.getItem("smarthotel_token");
          if (!isJwt(validatedToken)) {
            throw new Error("Session validation did not produce a valid access token.");
          }
          set({ token: validatedToken, user, isAuthenticated: true, mustResetPassword: mustReset });
        } catch {
          localStorage.removeItem("smarthotel_token");
          localStorage.removeItem("smarthotel_refresh_token");
          localStorage.removeItem("smarthotel_user");
          localStorage.removeItem("smarthotel_must_reset_password");
          set({ token: null, user: null, isAuthenticated: false, mustResetPassword: false });
        }
      }
    }
  },
}));
