"use client";

import { StaffProfileModal } from "@/components/staff/StaffProfileModal";
import { dashboardApi,type EmployeeCreateDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import { AlertCircle,Check,Copy,Eye,KeyRound,Loader2,Plus,RefreshCw,X } from "lucide-react";
import { useState } from "react";

type Credentials = { email: string; temporaryPassword: string; message: string };

function CredentialsModal({ credentials, onClose }: { credentials: Credentials; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  const copyPassword = async () => {
    await navigator.clipboard.writeText(credentials.temporaryPassword);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1800);
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/65 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] shadow-2xl">
        <div className="flex items-center justify-between border-b border-[var(--card-border)] p-5">
          <div className="flex items-center gap-2 text-[var(--text-primary)]"><KeyRound className="h-5 w-5 text-[#E87332]" /><h2 className="font-semibold">Temporary credentials</h2></div>
          <button onClick={onClose} className="rounded-lg p-1.5 text-[var(--text-secondary)] hover:bg-white/5"><X className="h-4 w-4" /></button>
        </div>
        <div className="space-y-4 p-5">
          <p className="text-sm text-[var(--text-secondary)]">{credentials.message}</p>
          <div className="rounded-xl border border-[#E87332]/25 bg-[#0F1B1A] p-4">
            <p className="text-xs text-[var(--text-secondary)]">Login email</p>
            <p className="mt-1 text-sm text-[var(--text-primary)]">{credentials.email}</p>
            <p className="mt-4 text-xs text-[var(--text-secondary)]">One-time temporary password</p>
            <div className="mt-1 flex items-center justify-between gap-3">
              <code className="break-all text-base font-bold tracking-wider text-[#F28A4B]">{credentials.temporaryPassword}</code>
              <button onClick={copyPassword} className="shrink-0 rounded-lg border border-white/10 p-2 text-white hover:bg-white/5" aria-label="Copy temporary password"><Copy className="h-4 w-4" /></button>
            </div>
          </div>
          {copied && <p className="text-xs text-[#9BC5B8]">Password copied.</p>}
          <p className="text-xs text-[var(--text-secondary)]">This password is displayed only now. The administrator must change it after the first login.</p>
          <button onClick={onClose} className="w-full rounded-xl bg-[#9F4724] px-4 py-2.5 text-sm font-medium text-white hover:bg-[#C4622D]">Done</button>
        </div>
      </div>
    </div>
  );
}

function AddAdminModal({ onClose, onCreated }: { onClose: () => void; onCreated: (credentials: Credentials) => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ firstName: "", lastName: "", email: "", departmentId: "" });
  const { data: departments = [] } = useQuery({ queryKey: ["departments"], queryFn: () => dashboardApi.getDepartments() });
  const mutation = useMutation({
    mutationFn: () => dashboardApi.createEmployee({ ...form, role: "Admin" }),
    onSuccess: async (admin) => {
      await queryClient.invalidateQueries({ queryKey: ["owner-admins"] });
      onClose();
      onCreated({ email: admin.email, temporaryPassword: admin.temporaryPassword || "", message: `Account created. Credentials email was submitted to ${admin.email}.` });
    },
  });
  const input = "w-full px-3 py-2 text-sm rounded-xl bg-[var(--bg-dark)] border border-[var(--card-border)] text-[var(--text-primary)] focus:outline-none focus:ring-2 focus:ring-[#C4622D]/40";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] shadow-2xl">
        <div className="flex items-center justify-between border-b border-[var(--card-border)] p-6"><h2 className="font-bold text-[var(--text-primary)]">Add Admin</h2><button onClick={onClose} className="p-1.5 text-[var(--text-secondary)]"><X className="h-4 w-4" /></button></div>
        <form onSubmit={(event) => { event.preventDefault(); mutation.mutate(); }} className="space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3"><input required placeholder="First name" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} className={input} /><input required placeholder="Last name" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} className={input} /></div>
          <input required type="email" placeholder="Login email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className={input} />
          <select required value={form.departmentId} onChange={(e) => setForm({ ...form, departmentId: e.target.value })} className={input}><option value="">Select department</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select>
          <div className="rounded-xl border border-[#2F5C52]/20 bg-[#2F5C52]/10 p-3 text-xs text-[var(--text-secondary)]">A secure password will be generated, emailed and shown once after creation.</div>
          {mutation.error && <p className="text-xs text-[#E87332]">{(mutation.error as { response?: { data?: { message?: string } } }).response?.data?.message ?? "Failed to create admin."}</p>}
          <div className="flex justify-end gap-3"><button type="button" onClick={onClose} className="px-4 py-2 text-sm text-[var(--text-secondary)]">Cancel</button><button disabled={mutation.isPending} className="flex items-center gap-2 rounded-xl bg-[#9F4724] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#C4622D] disabled:opacity-60">{mutation.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />}Create Admin</button></div>
        </form>
      </div>
    </div>
  );
}

export default function OwnerAdminsPage() {
  const [showAdd, setShowAdd] = useState(false);
  const [credentials, setCredentials] = useState<Credentials | null>(null);
  const [selectedAdmin, setSelectedAdmin] = useState<EmployeeCreateDto | null>(null);
  const {
    data: employees = [],
    isLoading,
    isFetching,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ["owner-admins"],
    queryFn: () => dashboardApi.getEmployees(),
    staleTime: 0,
    refetchOnMount: "always",
  });
  const admins = employees.filter((employee) => String(employee.role).trim().toLowerCase() === "admin");
  const errorMessage =
    (error as { friendlyMessage?: string; response?: { data?: { message?: string } } } | null)?.friendlyMessage ??
    (error as { response?: { data?: { message?: string } } } | null)?.response?.data?.message ??
    "Could not load administrators from the Identity service.";

  return (
    <div className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      <div className="flex items-center justify-between"><div><h1 className="text-3xl font-bold text-[var(--text-primary)]">Administrators</h1><p className="mt-1 text-sm text-[var(--text-secondary)]">{isLoading ? "Loading administrator accounts…" : `${admins.length} active and pending administrator accounts`}</p></div><button onClick={() => setShowAdd(true)} className="flex items-center gap-2 rounded-xl bg-[#9F4724] px-4 py-2.5 text-sm font-medium text-white hover:bg-[#C4622D]"><Plus className="h-4 w-4" />Add Admin</button></div>
      {isError && (
        <div className="flex items-center justify-between gap-4 rounded-xl border border-[#E87332]/35 bg-[#E87332]/10 p-4 text-sm text-[#F2A276]">
          <div className="flex items-center gap-2"><AlertCircle className="h-4 w-4 shrink-0" /><span>{errorMessage}</span></div>
          <button type="button" onClick={() => void refetch()} disabled={isFetching} className="flex shrink-0 items-center gap-2 rounded-lg border border-[#E87332]/30 px-3 py-1.5 text-xs font-medium hover:bg-[#E87332]/10 disabled:opacity-60"><RefreshCw className={`h-3.5 w-3.5 ${isFetching ? "animate-spin" : ""}`} />Retry</button>
        </div>
      )}
      <div className="overflow-x-auto rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]">
        <table className="w-full text-sm"><thead><tr className="border-b border-[var(--card-border)]">{["Administrator", "Email", "Department", "Status", "Details"].map((heading) => <th key={heading} className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)]">{heading}</th>)}</tr></thead>
          <tbody>{isLoading ? [...Array(4)].map((_, index) => <tr key={index} className="animate-pulse border-b border-[var(--card-border)]">{[...Array(5)].map((__, cell) => <td key={cell} className="px-4 py-3"><div className="h-4 w-3/4 rounded bg-white/8" /></td>)}</tr>) : admins.map((admin) => <tr key={admin.id} className="border-b border-[var(--card-border)] hover:bg-white/2"><td className="px-4 py-3 font-medium text-[var(--text-primary)]">{admin.fullName}</td><td className="px-4 py-3 text-[var(--text-secondary)]">{admin.email}</td><td className="px-4 py-3 text-[var(--text-secondary)]">{admin.departmentName}</td><td className="px-4 py-3"><span className="rounded-full border border-[#2F5C52]/30 bg-[#2F5C52]/15 px-2.5 py-0.5 text-xs text-[#9BC5B8]">{admin.status}</span></td><td className="px-4 py-3"><button onClick={() => setSelectedAdmin(admin)} className="flex items-center gap-1.5 rounded-lg border border-[#E87332]/25 px-3 py-1.5 text-xs text-[#F28A4B] hover:bg-[#C4622D]/15"><Eye className="h-3.5 w-3.5" />View details</button></td></tr>)}</tbody>
        </table>
      </div>
      {showAdd && <AddAdminModal onClose={() => setShowAdd(false)} onCreated={setCredentials} />}
      {credentials && <CredentialsModal credentials={credentials} onClose={() => setCredentials(null)} />}
      {selectedAdmin && <StaffProfileModal employee={selectedAdmin} queryKey="owner-admins" canEditProfile={false} onClose={() => setSelectedAdmin(null)} onCredentials={setCredentials} />}
    </div>
  );
}
