"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { dashboardApi,type CreateDepartmentPayload,type DepartmentDto,type EmployeeCreateDto } from "@/services/dashboardApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import { Building2,ChevronDown,ChevronUp,Loader2,Pencil,Plus,UserCog,Users,X } from "lucide-react";
import { useMemo,useState } from "react";

function DepartmentForm({ department, managers, onClose }: {
  department?: DepartmentDto;
  managers: EmployeeCreateDto[];
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<CreateDepartmentPayload>({
    name: department?.name ?? "",
    description: department?.description ?? "",
    managerId: department?.managerId ?? null,
  });

  const mutation = useMutation({
    mutationFn: () => department
      ? dashboardApi.updateDepartment(department.id, form)
      : dashboardApi.createDepartment(form),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["departments"] }),
        queryClient.invalidateQueries({ queryKey: ["employees"] }),
      ]);
      onClose();
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/65 p-4 backdrop-blur-sm">
      <form onSubmit={(event) => { event.preventDefault(); mutation.mutate(); }} className="w-full max-w-lg space-y-4 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-6 shadow-2xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-[var(--text-primary)]">{department ? "Edit Department" : "Create Department"}</h2>
          <button type="button" onClick={onClose}><X className="h-4 w-4" /></button>
        </div>
        <label className="block text-xs text-[var(--text-secondary)]">Name
          <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="mt-1 w-full rounded-xl border border-[var(--card-border)] bg-[var(--background)] px-3 py-2 text-sm text-[var(--text-primary)]" />
        </label>
        <label className="block text-xs text-[var(--text-secondary)]">Description
          <textarea rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="mt-1 w-full rounded-xl border border-[var(--card-border)] bg-[var(--background)] px-3 py-2 text-sm text-[var(--text-primary)]" />
        </label>
        <label className="block text-xs text-[var(--text-secondary)]">Manager
          <select value={form.managerId ?? ""} onChange={(e) => setForm({ ...form, managerId: e.target.value || null })} className="mt-1 w-full rounded-xl border border-[var(--card-border)] bg-[var(--background)] px-3 py-2 text-sm text-[var(--text-primary)]">
            <option value="">No manager assigned</option>
            {managers.map((manager) => <option key={manager.id} value={manager.id}>{manager.fullName || `${manager.firstName} ${manager.lastName}`}</option>)}
          </select>
        </label>
        {mutation.error && <p className="text-sm text-red-500">{(mutation.error as { response?: { data?: { message?: string } } }).response?.data?.message ?? "Could not save department."}</p>}
        <div className="flex justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-xl px-4 py-2 text-sm">Cancel</button>
          <button disabled={mutation.isPending} className="flex items-center gap-2 rounded-xl bg-[#C4622D] px-4 py-2 text-sm text-white disabled:opacity-60">
            {mutation.isPending && <Loader2 className="h-4 w-4 animate-spin" />} Save
          </button>
        </div>
      </form>
    </div>
  );
}

function DepartmentCard({ department, canEdit, onEdit }: { department: DepartmentDto; canEdit: boolean; onEdit: () => void }) {
  const [expanded, setExpanded] = useState(false);
  const initials = department.managerName?.split(" ").map((part) => part[0]).slice(0, 2).join("").toUpperCase();

  return (
    <article className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-5">
      <div className="flex items-start justify-between gap-3">
        <div><h3 className="font-semibold text-[var(--text-primary)]">{department.name}</h3><p className="mt-1 text-xs text-[var(--text-secondary)]">{department.description}</p></div>
        {canEdit && <button onClick={onEdit} aria-label={`Edit ${department.name}`} className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs text-[#C4622D] hover:bg-white/5">{department.managerId ? <Pencil className="h-3.5 w-3.5" /> : <UserCog className="h-3.5 w-3.5" />}{department.managerId ? "Edit" : "Assign Manager"}</button>}
      </div>
      <div className="mt-4 flex items-center gap-3 rounded-xl bg-[var(--background)] p-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-full bg-[#2F5C52]/20 text-xs font-bold text-[#2F5C52]">{initials || <UserCog className="h-4 w-4" />}</div>
        <div><p className="text-[10px] uppercase tracking-wide text-[var(--text-secondary)]">Manager</p><p className="text-sm text-[var(--text-primary)]">{department.managerName || "No manager assigned"}</p></div>
      </div>
      <div className="mt-4 flex items-center justify-between text-sm text-[var(--text-secondary)]">
        <span className="flex items-center gap-2"><Users className="h-4 w-4" />{department.employeeCount} employees</span>
        <button onClick={() => setExpanded(!expanded)} className="flex items-center gap-1 text-[#C4622D]">View Employees {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}</button>
      </div>
      {expanded && <ul className="mt-3 divide-y divide-[var(--card-border)] border-t border-[var(--card-border)]">{department.employees.length ? department.employees.map((employee) => <li key={employee.id} className="flex justify-between py-2 text-xs"><span>{employee.fullName}</span><span className="text-[var(--text-secondary)]">{employee.role}</span></li>) : <li className="py-3 text-xs text-[var(--text-secondary)]">No employees in this department.</li>}</ul>}
    </article>
  );
}

export default function DepartmentsPage() {
  const { user } = useAuthStore();
  const role = String(user?.role ?? "");
  const canEdit = role === "Owner" || role === "Admin";
  const canCreate = role === "Owner";
  const [editing, setEditing] = useState<DepartmentDto | null | undefined>(undefined);
  const { data: departments = [], isLoading } = useQuery({ queryKey: ["departments"], queryFn: () => dashboardApi.getDepartments() });
  const { data: employees = [] } = useQuery({ queryKey: ["employees"], queryFn: () => dashboardApi.getEmployees(), enabled: canEdit });
  const usedManagerIds = useMemo(() => new Set(departments.map((department) => department.managerId).filter(Boolean)), [departments]);
  const availableManagers = employees.filter((employee) => employee.role === "Manager" && (!usedManagerIds.has(employee.id) || editing?.managerId === employee.id));

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-6 lg:p-8">
      <header className="flex items-center justify-between"><div><h1 className="text-3xl font-bold text-[var(--text-primary)]">Departments</h1><p className="text-sm text-[var(--text-secondary)]">One active Admin and Manager per department.</p></div>{canCreate && <button onClick={() => setEditing(null)} className="flex items-center gap-2 rounded-xl bg-[#C4622D] px-4 py-2.5 text-sm text-white"><Plus className="h-4 w-4" />New Department</button>}</header>
      {isLoading ? <div className="flex items-center gap-2 text-sm"><Loader2 className="h-4 w-4 animate-spin" />Loading departments…</div> : departments.length === 0 ? <div className="rounded-2xl border border-dashed border-[var(--card-border)] p-12 text-center"><Building2 className="mx-auto mb-3 h-8 w-8" /><p>No departments configured.</p></div> : <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">{departments.map((department) => <DepartmentCard key={department.id} department={department} canEdit={canEdit} onEdit={() => setEditing(department)} />)}</div>}
      {editing !== undefined && <DepartmentForm department={editing ?? undefined} managers={availableManagers} onClose={() => setEditing(undefined)} />}
    </main>
  );
}
