"use client";

import { StaffProfileModal } from "@/components/staff/StaffProfileModal";
import { useAuthStore } from "@/features/auth/store/useAuthStore";
import api from "@/lib/axios";
import type { EmployeeCreateDto } from "@/services/dashboardApi";
import { dashboardApi } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertCircle,
BadgeCheck,
Check,
ChevronDown,
Clock,
Eye,
Loader2,
Mail,
Search,
ShieldAlert,
UserCog,
UserPlus,
Users,
X,
XCircle,
} from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

// ── Shared Employee DTO (list endpoint) ──────────────────────────────────────

interface EmployeeDto {
  id: string;
  firstName?: string;
  lastName?: string;
  fullName?: string;
  email: string;
  role?: string | number;
  status?: string; // "Active" | "PendingApproval" | "Rejected"
  departmentName?: string;
  isActive?: boolean;
  joinedAt?: string;
  createdByName?: string;
  rejectionReason?: string;
}

// ── Role + Status helpers ─────────────────────────────────────────────────────

const EMPLOYEE_ROLES = [
  { value: "Manager", label: "Manager", color: "bg-[#6B4A3A]/15 text-[#6B4A3A] border-[#6B4A3A]/30" },
  { value: "Receptionist", label: "Front Desk", color: "bg-[#2F5C52]/15 text-[#2F5C52] border-[#2F5C52]/30" },
  { value: "Housekeeper", label: "Housekeeping", color: "bg-blue-500/15 text-blue-600 border-blue-400/30" },
  { value: "Maintenance", label: "Maintenance", color: "bg-amber-500/15 text-amber-600 border-amber-400/30" },
  { value: "Chef", label: "Kitchen", color: "bg-purple-500/15 text-purple-600 border-purple-400/30" },
  { value: "Waiter", label: "Waiter", color: "bg-pink-500/15 text-pink-600 border-pink-400/30" },
  { value: "Security", label: "Security", color: "bg-red-500/15 text-red-600 border-red-400/30" },
];

// Roles a Manager can assign (no Manager/Admin)
const MANAGER_ASSIGNABLE_ROLES = EMPLOYEE_ROLES.filter(
  (r) => r.value !== "Manager"
);

function roleColor(role?: string | number): string {
  const r = String(role ?? "");
  return (
    EMPLOYEE_ROLES.find((x) => x.value === r)?.color ??
    "bg-gray-500/15 text-gray-500 border-gray-400/30"
  );
}

function roleLabel(role?: string | number): string {
  const r = String(role ?? "");
  return (EMPLOYEE_ROLES.find((x) => x.value === r)?.label ?? r) || "Staff";
}

function statusBadge(status?: string): { label: string; className: string; Icon: React.ElementType } {
  switch (status) {
    case "Active":
      return {
        label: "Active",
        className: "bg-[#2F5C52]/15 text-[#2F5C52] border-[#2F5C52]/30",
        Icon: BadgeCheck,
      };
    case "PendingApproval":
      return {
        label: "Pending Approval",
        className: "bg-[#C4622D]/10 text-[#C4622D] border-[#C4622D]/30",
        Icon: Clock,
      };
    case "Rejected":
      return {
        label: "Rejected",
        className: "bg-[#C4622D]/15 text-[#C4622D]/70 border-[#C4622D]/20 line-through",
        Icon: XCircle,
      };
    default:
      return {
        label: "Unknown",
        className: "bg-gray-500/10 text-gray-500 border-gray-400/20",
        Icon: AlertCircle,
      };
  }
}

function getName(e: EmployeeDto | EmployeeCreateDto): string {
  if ("fullName" in e && e.fullName) return e.fullName;
  const first = "firstName" in e ? (e.firstName ?? "") : "";
  const last = "lastName" in e ? (e.lastName ?? "") : "";
  return `${first} ${last}`.trim() || ("email" in e ? e.email : "");
}

// ── Create Employee Modal ─────────────────────────────────────────────────────

function CreateEmployeeModal({
  isAdmin,
  departments,
  onClose,
  onCreated,
}: {
  isAdmin: boolean;
  departments: { id: string; name: string }[];
  onClose: () => void;
  onCreated: (createdEmail: string) => void;
}) {
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    email: "",
    role: isAdmin ? "Receptionist" : "Receptionist",
    departmentId: "",
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const assignableRoles = isAdmin ? EMPLOYEE_ROLES : MANAGER_ASSIGNABLE_ROLES;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.createEmployee({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        role: form.role,
        departmentId: form.departmentId,
      });
      onCreated(form.email.trim());
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to create employee.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  const title = isAdmin ? "Add Manager / Employee" : "Add Employee";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-md">
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2 text-[#2F5C52]">
            <UserPlus className="w-4 h-4" />
            <span className="font-bold text-sm" style={{ fontFamily: "var(--font-fraunces), serif" }}>
              {title}
            </span>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          {/* Info banner */}
          <div className="p-3.5 rounded-xl bg-[#2F5C52]/10 border border-[#2F5C52]/20 text-xs text-[var(--text-secondary)] space-y-1">
            <div className="font-semibold text-[#2F5C52] dark:text-[#529487]">
              {isAdmin ? "Instant Activation & Credential Delivery" : "Submitted for Admin Approval"}
            </div>
            <p className="leading-relaxed">
              {isAdmin
                ? "A cryptographically secure password will be auto-generated and emailed to the address provided. Mandatory first-login password reset is enforced."
                : "Submitted for Administrator approval. Upon approval, secure login credentials will be generated and emailed automatically."}
            </p>
          </div>

          {/* Name row */}
          <div className="grid grid-cols-2 gap-3">
            {(["firstName", "lastName"] as const).map((k) => (
              <div key={k}>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  {k === "firstName" ? "First Name" : "Last Name"} *
                </label>
                <input
                  required
                  value={form[k]}
                  onChange={set(k)}
                  placeholder={k === "firstName" ? "Amal" : "Perera"}
                  className="w-full px-3 py-2 text-sm bg-[var(--background)] border border-[var(--card-border)] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#2F5C52]/30 text-[var(--text-primary)]"
                />
              </div>
            ))}
          </div>

          {/* Email */}
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Login Email *</label>
            <input
              type="email"
              required
              value={form.email}
              onChange={set("email")}
              placeholder="amal@smarthotel.internal"
              className="w-full px-3 py-2 text-sm bg-[var(--background)] border border-[var(--card-border)] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#2F5C52]/30 text-[var(--text-primary)]"
            />
          </div>

          {/* Role picker */}
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-2">Role *</label>
            <div className="grid grid-cols-2 gap-2">
              {assignableRoles.map((r) => (
                <button
                  key={r.value}
                  type="button"
                  onClick={() => setForm((f) => ({ ...f, role: r.value }))}
                  className={`px-3 py-2 rounded-xl text-xs font-medium border transition-all cursor-pointer ${
                    form.role === r.value
                      ? r.color + " ring-2 ring-offset-1 ring-[#2F5C52]/40"
                      : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#2F5C52]/20"
                  }`}
                >
                  {r.label}
                </button>
              ))}
            </div>
          </div>

          {/* Department */}
          <div>
              <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Department *</label>
              <div className="relative">
                <select
                  required
                  value={form.departmentId}
                  onChange={set("departmentId")}
                  className="w-full px-3 py-2 text-sm bg-[var(--background)] border border-[var(--card-border)] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#2F5C52]/30 text-[var(--text-primary)] appearance-none pr-8"
                >
                  <option value="">Select a department</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>{d.name}</option>
                  ))}
                </select>
                <ChevronDown className="absolute right-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-secondary)] pointer-events-none" />
              </div>
            </div>

          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}

          <div className="flex items-center gap-2 justify-end pt-1">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-xl cursor-pointer">
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#2F5C52] hover:bg-[#3A7A6E] text-white rounded-xl transition-colors disabled:opacity-50 cursor-pointer"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <UserPlus className="w-3.5 h-3.5" />}
              {isAdmin ? "Create" : "Submit for Approval"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Reject Confirm Modal ──────────────────────────────────────────────────────

function RejectModal({
  employee,
  onClose,
  onConfirm,
}: {
  employee: EmployeeCreateDto;
  onClose: () => void;
  onConfirm: (reason?: string) => void;
}) {
  const [reason, setReason] = useState("");
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-sm">
        <div className="flex items-center justify-between p-5 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2 text-[#C4622D]">
            <ShieldAlert className="w-4 h-4" />
            <span className="font-bold text-sm" style={{ fontFamily: "var(--font-fraunces), serif" }}>Reject Employee</span>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-5 space-y-4">
          <p className="text-sm text-[var(--text-secondary)]">
            Reject <strong className="text-[var(--text-primary)]">{getName(employee)}</strong>? The record will be kept for audit but they cannot log in.
          </p>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">Reason (optional)</label>
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Position no longer available"
              className="w-full px-3 py-2 text-sm bg-[var(--background)] border border-[var(--card-border)] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#C4622D]/30 text-[var(--text-primary)]"
            />
          </div>
          <div className="flex items-center gap-2 justify-end">
            <button onClick={onClose} className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-xl cursor-pointer">
              Cancel
            </button>
            <button
              onClick={() => onConfirm(reason || undefined)}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-xl transition-colors cursor-pointer"
            >
              <XCircle className="w-3.5 h-3.5" /> Reject
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Role Update Modal ─────────────────────────────────────────────────────────

function RoleUpdateModal({
  employee,
  onClose,
  onUpdated,
}: {
  employee: EmployeeDto;
  onClose: () => void;
  onUpdated: () => void;
}) {
  const [newRole, setNewRole] = useState(String(employee.role ?? "Receptionist"));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const currentRole = String(employee.role ?? "");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newRole === currentRole) { onClose(); return; }
    setLoading(true);
    setError(null);
    try {
      await dashboardApi.updateEmployeeRole(employee.id, newRole);
      onUpdated();
      onClose();
    } catch {
      setError("Failed to update role. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const name = getName(employee);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="bg-[var(--card-dark)] rounded-2xl border border-[var(--card-border)] shadow-2xl w-full max-w-sm">
        <div className="flex items-center justify-between p-5 border-b border-[var(--card-border)]">
          <div className="flex items-center gap-2 text-[#C4622D]">
            <ShieldAlert className="w-4 h-4" />
            <span className="font-bold text-sm" style={{ fontFamily: "var(--font-fraunces), serif" }}>Change Role</span>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[var(--text-primary)]/8 text-[var(--text-secondary)] cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          <div className="p-3 rounded-xl bg-[#C4622D]/5 border border-[#C4622D]/15 text-sm text-[var(--text-secondary)]">
            <p><span className="font-semibold text-[var(--text-primary)]">{name}</span></p>
            <p className="text-xs mt-0.5">{employee.email}</p>
          </div>
          <div>
            <label className="block text-xs font-medium text-[var(--text-secondary)] mb-2">New Role</label>
            <div className="grid grid-cols-2 gap-2">
              {EMPLOYEE_ROLES.map((r) => (
                <button
                  key={r.value}
                  type="button"
                  onClick={() => setNewRole(r.value)}
                  className={`px-3 py-2 rounded-xl text-xs font-medium border transition-all cursor-pointer ${
                    newRole === r.value ? r.color + " ring-2 ring-offset-1 ring-[#C4622D]/40" : "border-[var(--card-border)] text-[var(--text-secondary)] hover:border-[#C4622D]/20"
                  }`}
                >
                  {r.label}
                </button>
              ))}
            </div>
          </div>
          {newRole !== currentRole && (
            <div className="flex items-start gap-2 text-xs text-amber-600 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/40 px-3 py-2.5 rounded-xl">
              <AlertCircle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
              <span>This will change <strong>{name}&apos;s</strong> role from <strong>{roleLabel(currentRole)}</strong> to <strong>{roleLabel(newRole)}</strong>. Takes effect on next login.</span>
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 text-xs text-red-500 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 px-3 py-2 rounded-lg">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
            </div>
          )}
          <div className="flex items-center gap-2 justify-end pt-1">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--text-primary)]/5 rounded-xl cursor-pointer">
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || newRole === currentRole}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium bg-[#C4622D] hover:bg-[#E07A3E] text-white rounded-xl transition-colors disabled:opacity-50 cursor-pointer"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
              Confirm Change
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ── Employee Card ─────────────────────────────────────────────────────────────

function EmployeeCard({
  employee,
  isAdmin,
  onManageRole,
  onViewDetails,
}: {
  employee: EmployeeDto;
  isAdmin: boolean;
  onManageRole: (e: EmployeeDto) => void;
  onViewDetails: (e: EmployeeDto) => void;
}) {
  const name = getName(employee);
  const initials = name.split(" ").slice(0, 2).map((w) => w[0] ?? "").join("").toUpperCase() || "?";
  const sb = statusBadge(employee.status);
  const StatusIcon = sb.Icon;
  const isRejected = employee.status === "Rejected";

  return (
    <div className={`flex items-start gap-4 p-4 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl hover:shadow-md hover:-translate-y-0.5 transition-all duration-200 ${isRejected ? "opacity-50" : ""}`}>
      <div className="w-11 h-11 rounded-xl bg-[#2F5C52]/10 border border-[#2F5C52]/20 flex items-center justify-center shrink-0">
        <span className="text-sm font-bold text-[#2F5C52]">{initials}</span>
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className={`text-sm font-semibold text-[var(--text-primary)] truncate ${isRejected ? "line-through" : ""}`}>{name}</p>
            <p className="text-xs text-[var(--text-secondary)] truncate">{employee.email}</p>
          </div>
          <div className="flex flex-col items-end gap-1 shrink-0">
            <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-semibold border ${roleColor(employee.role)}`}>
              {roleLabel(employee.role)}
            </span>
            <span className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-[9px] font-semibold border ${sb.className}`}>
              <StatusIcon className="w-2.5 h-2.5" />
              {sb.label}
            </span>
          </div>
        </div>
        {(employee.departmentName || isAdmin) && (
          <div className="flex items-center justify-between mt-2">
            {employee.departmentName && (
              <span className="text-xs text-[var(--text-secondary)]">{employee.departmentName}</span>
            )}
            {isAdmin && employee.status === "Active" && (
              <button
                onClick={() => onManageRole(employee)}
                className="flex items-center gap-1 text-xs font-medium text-[#C4622D] hover:underline cursor-pointer ml-auto"
              >
                <UserCog className="w-3 h-3" /> Change role
              </button>
            )}
            {isAdmin && (
              <button onClick={() => onViewDetails(employee)} className="ml-3 flex items-center gap-1 text-xs font-medium text-[#F28A4B] hover:underline">
                <Eye className="h-3 w-3" /> View details
              </button>
            )}
          </div>
        )}
        {employee.status === "Rejected" && employee.rejectionReason && (
          <p className="mt-1.5 text-[10px] text-[#C4622D]/70 italic">Reason: {employee.rejectionReason}</p>
        )}
      </div>
    </div>
  );
}

// ── Pending Approval Card ─────────────────────────────────────────────────────

function PendingCard({
  employee,
  onApprove,
  onReject,
  approving,
  rejecting,
}: {
  employee: EmployeeCreateDto;
  onApprove: () => void;
  onReject: () => void;
  approving: boolean;
  rejecting: boolean;
}) {
  const name = getName(employee);
  const initials = name.split(" ").slice(0, 2).map((w) => w[0] ?? "").join("").toUpperCase() || "?";

  return (
    <div className="flex items-start gap-4 p-4 bg-[var(--card-dark)] border border-[#C4622D]/20 rounded-2xl">
      <div className="w-11 h-11 rounded-xl bg-[#C4622D]/10 border border-[#C4622D]/20 flex items-center justify-center shrink-0">
        <span className="text-sm font-bold text-[#C4622D]">{initials}</span>
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="text-sm font-semibold text-[var(--text-primary)] truncate">{name}</p>
            <p className="text-xs text-[var(--text-secondary)] truncate">{employee.email}</p>
          </div>
          <span className={`shrink-0 px-2.5 py-0.5 rounded-full text-[10px] font-semibold border ${roleColor(employee.role)}`}>
            {roleLabel(employee.role)}
          </span>
        </div>
        {employee.createdByName && (
          <p className="mt-1 text-xs text-[var(--text-secondary)]">
            Created by: <span className="font-medium text-[var(--text-primary)]">{employee.createdByName}</span>
          </p>
        )}
        {employee.departmentName && (
          <p className="text-xs text-[var(--text-secondary)]">{employee.departmentName}</p>
        )}
        <div className="flex items-center gap-2 mt-3">
          <button
            onClick={onApprove}
            disabled={approving || rejecting}
            className="flex items-center gap-1.5 px-4 py-1.5 text-xs font-semibold bg-[#2F5C52] hover:bg-[#3A7A6E] text-white rounded-xl transition-colors disabled:opacity-50 cursor-pointer"
          >
            {approving ? <Loader2 className="w-3 h-3 animate-spin" /> : <Check className="w-3 h-3" />}
            Approve
          </button>
          <button
            onClick={onReject}
            disabled={approving || rejecting}
            className="flex items-center gap-1.5 px-4 py-1.5 text-xs font-semibold bg-[#C4622D]/10 hover:bg-[#C4622D]/20 text-[#C4622D] border border-[#C4622D]/30 rounded-xl transition-colors disabled:opacity-50 cursor-pointer"
          >
            {rejecting ? <Loader2 className="w-3 h-3 animate-spin" /> : <XCircle className="w-3 h-3" />}
            Reject
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

type TabId = "all" | "pending";

export default function EmployeesPage() {
  const { user } = useAuthStore();
  const qc = useQueryClient();
  const isAdmin = String(user?.role) === "Admin";
  const isManager = String(user?.role) === "Manager";

  const [activeTab, setActiveTab] = useState<TabId>("all");
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState<string>("");
  const [managingEmployee, setManagingEmployee] = useState<EmployeeDto | null>(null);
  const [viewingEmployee, setViewingEmployee] = useState<EmployeeDto | null>(null);
  const [shownCredentials, setShownCredentials] = useState<{ email: string; temporaryPassword: string; message: string } | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [rejectingEmployee, setRejectingEmployee] = useState<EmployeeCreateDto | null>(null);
  const [actionState, setActionState] = useState<Record<string, { approving?: boolean; rejecting?: boolean }>>({});
  const [toast, setToast] = useState<{ message: string; type?: "success" | "info" } | null>(null);

  React.useEffect(() => {
    if (toast) {
      const timer = setTimeout(() => setToast(null), 6000);
      return () => clearTimeout(timer);
    }
  }, [toast]);

  // ── Queries ─────────────────────────────────────────────────────────────────

  const { data: employees = [], isLoading } = useQuery<EmployeeDto[]>({
    queryKey: ["employees"],
    queryFn: async () => {
      const { data } = await api.get<EmployeeDto[]>("/api/v1/employees");
      return data;
    },
    staleTime: 60_000,
  });

  const { data: pendingEmployees = [], isLoading: pendingLoading } = useQuery<EmployeeCreateDto[]>({
    queryKey: ["employees-pending"],
    queryFn: () => dashboardApi.getPendingEmployees(),
    enabled: isAdmin,
    staleTime: 30_000,
  });

  // ── Departments for create form ──────────────────────────────────────────────

  const { data: departments = [] } = useQuery<{ id: string; name: string }[]>({
    queryKey: ["departments-simple"],
    queryFn: async () => {
      const { data } = await api.get<{ id: string; name: string }[]>("/api/v1/departments");
      return data;
    },
    staleTime: 120_000,
  });

  // ── Approve mutation ─────────────────────────────────────────────────────────

  const approveMutation = useMutation({
    mutationFn: (id: string) => dashboardApi.approveEmployee(id),
    onMutate: (id) => setActionState((s) => ({ ...s, [id]: { ...s[id], approving: true } })),
    onSuccess: (data) => {
      setToast({
        type: "success",
        message: `Employee approved — login credentials sent to ${data.email}`,
      });
    },
    onSettled: (_, __, id) => {
      setActionState((s) => ({ ...s, [id]: { ...s[id], approving: false } }));
      qc.invalidateQueries({ queryKey: ["employees"] });
      qc.invalidateQueries({ queryKey: ["employees-pending"] });
    },
  });

  // ── Reject mutation ──────────────────────────────────────────────────────────

  const rejectMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      dashboardApi.rejectEmployee(id, reason),
    onMutate: ({ id }) => setActionState((s) => ({ ...s, [id]: { ...s[id], rejecting: true } })),
    onSuccess: () => {
      setToast({
        type: "info",
        message: "Employee application rejected.",
      });
    },
    onSettled: (_, __, { id }) => {
      setActionState((s) => ({ ...s, [id]: { ...s[id], rejecting: false } }));
      setRejectingEmployee(null);
      qc.invalidateQueries({ queryKey: ["employees"] });
      qc.invalidateQueries({ queryKey: ["employees-pending"] });
    },
  });

  // ── Filter ───────────────────────────────────────────────────────────────────

  const filtered = employees.filter((e) => {
    const name = getName(e).toLowerCase();
    const matchesSearch =
      !search || name.includes(search.toLowerCase()) || e.email.toLowerCase().includes(search.toLowerCase());
    const matchesRole = !roleFilter || String(e.role) === roleFilter;
    return matchesSearch && matchesRole;
  });

  // ── Pending count badge ───────────────────────────────────────────────────────

  const pendingCount = pendingEmployees.length;

  return (
    <div className="p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1
            className="text-3xl font-bold text-[var(--text-primary)] tracking-tight"
            style={{ fontFamily: "var(--font-fraunces), serif" }}
          >
            Employees
          </h1>
          <p className="text-sm text-[var(--text-secondary)] mt-1">
            {employees.length} staff member{employees.length !== 1 ? "s" : ""}
            {isAdmin && pendingCount > 0 && (
              <span className="ml-2 px-2 py-0.5 rounded-full text-[10px] font-bold bg-[#C4622D]/15 text-[#C4622D] border border-[#C4622D]/30">
                {pendingCount} pending
              </span>
            )}
          </p>
        </div>

        {/* Create button — role-gated */}
        {(isAdmin || isManager) && (
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 px-5 py-2.5 text-sm font-semibold bg-[#2F5C52] hover:bg-[#3A7A6E] text-white rounded-2xl shadow-lg shadow-[#2F5C52]/20 transition-all hover:-translate-y-0.5 cursor-pointer"
          >
            <UserPlus className="w-4 h-4" />
            {isAdmin ? "Add Manager / Employee" : "Add Employee"}
          </button>
        )}
      </div>

      {/* Tabs — Admin sees pending tab */}
      {isAdmin && (
        <div className="flex items-center gap-1 p-1 bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl w-fit">
          {([
            { id: "all" as TabId, label: "All Staff", icon: Users },
            { id: "pending" as TabId, label: "Pending Approvals", icon: Clock, count: pendingCount },
          ]).map(({ id, label, icon: Icon, count }) => (
            <button
              key={id}
              onClick={() => setActiveTab(id)}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-xl transition-all cursor-pointer ${
                activeTab === id
                  ? "bg-[var(--background)] text-[var(--text-primary)] shadow-sm"
                  : "text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
              }`}
            >
              <Icon className="w-4 h-4" />
              {label}
              {count !== undefined && count > 0 && (
                <span className="px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-[#C4622D] text-white">
                  {count}
                </span>
              )}
            </button>
          ))}
        </div>
      )}

      {/* ── All Staff Tab ─────────────────────────────────────────────────── */}
      {activeTab === "all" && (
        <>
          {/* Filters */}
          <div className="flex flex-col sm:flex-row gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-secondary)]" />
              <input
                type="text"
                placeholder="Search by name or email…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full pl-9 pr-4 py-2.5 text-sm bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl focus:outline-none focus:ring-2 focus:ring-[#2F5C52]/30 text-[var(--text-primary)]"
              />
            </div>
            <div className="relative">
              <select
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value)}
                className="appearance-none pl-4 pr-8 py-2.5 text-sm bg-[var(--card-dark)] border border-[var(--card-border)] rounded-2xl focus:outline-none focus:ring-2 focus:ring-[#2F5C52]/30 text-[var(--text-primary)] cursor-pointer"
              >
                <option value="">All roles</option>
                {EMPLOYEE_ROLES.map((r) => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
              <ChevronDown className="absolute right-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-secondary)] pointer-events-none" />
            </div>
          </div>

          {/* Grid */}
          {isLoading ? (
            <div className="grid sm:grid-cols-2 gap-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="h-24 rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)] animate-pulse" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="relative rounded-2xl overflow-hidden min-h-[240px] flex items-end">
              <Image src="/images/ridge-thumb.jpg" alt="No employees" fill className="object-cover" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/70 to-transparent" />
              <div className="relative z-10 p-6 text-white">
                <Users className="w-8 h-8 mb-2 opacity-80" />
                <p className="font-semibold" style={{ fontFamily: "var(--font-fraunces), serif" }}>
                  {search || roleFilter ? "No employees match your filters." : "No employee records found."}
                </p>
              </div>
            </div>
          ) : (
            <div className="grid sm:grid-cols-2 gap-4">
              {filtered.map((e) => (
                <EmployeeCard
                  key={e.id}
                  employee={e}
                  isAdmin={isAdmin}
                  onManageRole={setManagingEmployee}
                  onViewDetails={setViewingEmployee}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* ── Pending Approvals Tab (Admin only) ───────────────────────────── */}
      {activeTab === "pending" && isAdmin && (
        <>
          {pendingLoading ? (
            <div className="grid sm:grid-cols-2 gap-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-32 rounded-2xl bg-[var(--card-dark)] border border-[var(--card-border)] animate-pulse" />
              ))}
            </div>
          ) : pendingEmployees.length === 0 ? (
            <div className="relative rounded-2xl overflow-hidden min-h-[200px] flex items-end">
              <Image src="/images/maskeliya-hero.jpg" alt="No pending" fill className="object-cover" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/70 to-transparent" />
              <div className="relative z-10 p-6 text-white">
                <BadgeCheck className="w-8 h-8 mb-2 opacity-80" />
                <p className="font-semibold" style={{ fontFamily: "var(--font-fraunces), serif" }}>
                  No pending approvals — you&apos;re all caught up!
                </p>
              </div>
            </div>
          ) : (
            <div className="grid sm:grid-cols-2 gap-4">
              {pendingEmployees.map((e) => (
                <PendingCard
                  key={e.id}
                  employee={e}
                  approving={actionState[e.id]?.approving ?? false}
                  rejecting={actionState[e.id]?.rejecting ?? false}
                  onApprove={() => approveMutation.mutate(e.id)}
                  onReject={() => setRejectingEmployee(e)}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* ── Modals ───────────────────────────────────────────────────────── */}
      {showCreate && (
        <CreateEmployeeModal
          isAdmin={isAdmin}
          departments={departments}
          onClose={() => setShowCreate(false)}
          onCreated={(createdEmail) => {
            qc.invalidateQueries({ queryKey: ["employees"] });
            qc.invalidateQueries({ queryKey: ["employees-pending"] });
            if (isAdmin) {
              setToast({
                type: "success",
                message: `Manager/Employee added — login credentials sent to ${createdEmail}`,
              });
            } else {
              setToast({
                type: "info",
                message: "Submitted for Admin approval.",
              });
            }
          }}
        />
      )}

      {managingEmployee && (
        <RoleUpdateModal
          employee={managingEmployee}
          onClose={() => setManagingEmployee(null)}
          onUpdated={() => qc.invalidateQueries({ queryKey: ["employees"] })}
        />
      )}

      {rejectingEmployee && (
        <RejectModal
          employee={rejectingEmployee}
          onClose={() => setRejectingEmployee(null)}
          onConfirm={(reason) =>
            rejectMutation.mutate({ id: rejectingEmployee.id, reason })
          }
        />
      )}

      {viewingEmployee && (
        <StaffProfileModal
          employee={{ ...viewingEmployee, fullName: getName(viewingEmployee) }}
          queryKey="employees"
          onClose={() => setViewingEmployee(null)}
          onCredentials={setShownCredentials}
        />
      )}

      {shownCredentials && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/70 p-4">
          <div className="w-full max-w-sm rounded-2xl border border-white/10 bg-[#16302C] p-5 text-white shadow-2xl">
            <h3 className="font-semibold">New temporary credentials</h3>
            <p className="mt-2 text-xs text-white/60">{shownCredentials.message}</p>
            <code className="mt-4 block rounded-xl bg-[#0F1B1A] p-4 text-base font-bold text-[#F28A4B]">{shownCredentials.temporaryPassword}</code>
            <div className="mt-4 flex gap-2"><button onClick={() => navigator.clipboard.writeText(shownCredentials.temporaryPassword)} className="flex-1 rounded-xl border border-white/10 px-3 py-2 text-sm">Copy</button><button onClick={() => setShownCredentials(null)} className="flex-1 rounded-xl bg-[#9F4724] px-3 py-2 text-sm">Done</button></div>
          </div>
        </div>
      )}

      {/* ── Toast Notification ─────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-3 px-4 py-3 bg-[#142322] border border-[#2F5C52]/60 rounded-2xl shadow-2xl text-xs text-[#E7EFEC] animate-in fade-in slide-in-from-bottom-5">
          <div className="w-7 h-7 rounded-xl bg-[#2F5C52]/20 border border-[#2F5C52]/40 flex items-center justify-center text-[#4ADE80] shrink-0">
            <Mail className="w-3.5 h-3.5" />
          </div>
          <span className="font-medium">{toast.message}</span>
          <button
            onClick={() => setToast(null)}
            className="p-1 hover:bg-white/10 rounded-lg text-[#8A9E99] hover:text-[#E7EFEC] transition-colors ml-2 cursor-pointer"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  );
}
