"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { getApiErrorMessage } from "@/lib/apiError";
import {
fnbApi,
type CreateStructuredOrderItem,
type FnbAuditLogDto,
type KdsOrderResponse,
type KdsStatus,
type MenuItemDto,
type OrderType,
} from "@/services/fnbApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertTriangle,
BedDouble,
CheckCircle2,
Eye,
Plus,
Receipt,
RefreshCw,
RotateCw,
Search,
ShieldAlert,
UtensilsCrossed,
XCircle
} from "lucide-react";
import { useMemo,useState } from "react";

export default function OrdersPage() {
  const user = useAuthStore((s) => s.user);
  const userRole = String(user?.role || "");
  const isManagerOrAdmin = ["Manager", "Admin", "Owner"].includes(userRole);

  const qc = useQueryClient();
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");

  // Filters
  const [statusFilter, setStatusFilter] = useState<string>("ALL");
  const [typeFilter, setTypeFilter] = useState<string>("ALL");
  const [searchQuery, setSearchQuery] = useState("");
  const [delayedOnly, setDelayedOnly] = useState(false);

  // Modals
  const [isNewOrderOpen, setIsNewOrderOpen] = useState(false);
  const [selectedOrderForAudit, setSelectedOrderForAudit] = useState<KdsOrderResponse | null>(null);
  const [cancelModalOrder, setCancelModalOrder] = useState<KdsOrderResponse | null>(null);
  const [cancelReason, setCancelReason] = useState("");

  // Data Queries
  const ordersQuery = useQuery({
    queryKey: ["fnb-orders", statusFilter, typeFilter, delayedOnly],
    queryFn: () =>
      fnbApi.getOrders({
        status: statusFilter !== "ALL" ? (statusFilter as KdsStatus) : undefined,
        orderType: typeFilter !== "ALL" ? (typeFilter as OrderType) : undefined,
        delayedOnly: delayedOnly ? true : undefined,
      }),
    refetchInterval: 12_000,
  });

  const summaryQuery = useQuery({
    queryKey: ["fnb-summary"],
    queryFn: () => fnbApi.getSummary(),
    refetchInterval: 20_000,
  });

  const menuQuery = useQuery({
    queryKey: ["fnb-menu"],
    queryFn: () => fnbApi.getMenu(),
    staleTime: 60_000,
  });

  const auditLogsQuery = useQuery({
    queryKey: ["fnb-audit", selectedOrderForAudit?.id],
    queryFn: () =>
      selectedOrderForAudit ? fnbApi.getAuditLogs(selectedOrderForAudit.id) : Promise.resolve([]),
    enabled: !!selectedOrderForAudit,
  });

  const orders = useMemo(() => ordersQuery.data ?? [], [ordersQuery.data]);
  const summary = summaryQuery.data;
  const menuItems = menuQuery.data || [];

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["fnb-orders"] });
    qc.invalidateQueries({ queryKey: ["fnb-summary"] });
  };

  // Status transition mutation
  const statusMutation = useMutation({
    mutationFn: async ({
      orderId,
      status,
      notes,
    }: {
      orderId: string;
      status: KdsStatus;
      notes?: string;
    }) => {
      setError("");
      setSuccessMsg("");
      return await fnbApi.updateStatus(orderId, { status, notes });
    },
    onSuccess: (data, vars) => {
      if (vars.status === "Delivered") {
        setSuccessMsg(
          `Order ${data.orderNumber} marked Delivered. Financial charge outbox queued for confirmation.`
        );
      }
      refresh();
    },
    onError: (err: unknown) => setError(getApiErrorMessage(err, "Action failed")),
  });

  // Retry Finance Charge mutation
  const retryChargeMutation = useMutation({
    mutationFn: async (orderId: string) => {
      setError("");
      setSuccessMsg("");
      return await fnbApi.retryFinanceCharge(orderId);
    },
    onSuccess: (res) => {
      if (res.success) {
        setSuccessMsg(
          `Finance charge reconciled successfully! Invoice #${res.invoiceNumber || res.invoiceId}`
        );
      } else {
        setError(res.errorMessage || "Finance charge attempt failed; queued for retry.");
      }
      refresh();
    },
    onError: (err: unknown) => setError(getApiErrorMessage(err, "Finance charge retry failed")),
  });

  // Cancel mutation
  const cancelMutation = useMutation({
    mutationFn: async ({ orderId, reason }: { orderId: string; reason: string }) => {
      setError("");
      setSuccessMsg("");
      return await fnbApi.cancelOrder(orderId, reason);
    },
    onSuccess: (data) => {
      setCancelModalOrder(null);
      setCancelReason("");
      setSuccessMsg(`Order ${data.orderNumber} cancelled with manager audit record.`);
      refresh();
    },
    onError: (err: unknown) => setError(getApiErrorMessage(err, "Cancellation failed")),
  });

  // Filtered orders with search query
  const filteredOrders = useMemo(() => {
    if (!searchQuery.trim()) return orders;
    const q = searchQuery.toLowerCase();
    return orders.filter(
      (o) =>
        o.orderNumber.toLowerCase().includes(q) ||
        o.tableOrRoomNumber.toLowerCase().includes(q) ||
        (o.customerName && o.customerName.toLowerCase().includes(q)) ||
        (o.roomNumber && o.roomNumber.toLowerCase().includes(q))
    );
  }, [orders, searchQuery]);

  return (
    <div className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#2F5C52]/20 text-[#2F5C52]">
              <UtensilsCrossed className="h-6 w-6 text-emerald-400" />
            </div>
            <div>
              <h1
                className="text-3xl font-bold tracking-tight text-white"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Food & Beverage Floor Station
              </h1>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                F&B order dispatching, floor delivery confirmation, and fail-safe Finance billing
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => refresh()}
            disabled={ordersQuery.isFetching}
            className="flex items-center gap-2 rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] px-3 py-2 text-sm text-[var(--text-secondary)] hover:text-white transition disabled:opacity-50"
            title="Refresh orders"
          >
            <RefreshCw
              className={`h-4 w-4 ${ordersQuery.isFetching ? "animate-spin text-emerald-400" : ""}`}
            />
            <span>Refresh</span>
          </button>
          <button
            onClick={() => setIsNewOrderOpen(true)}
            className="flex items-center gap-2 rounded-xl bg-[#2F5C52] px-4 py-2 text-sm font-semibold text-white hover:bg-[#254b42] shadow-lg shadow-[#2F5C52]/20 transition"
          >
            <Plus className="h-4 w-4" />
            <span>Create Order</span>
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">Today&apos;s Orders</span>
          <p className="mt-2 text-2xl font-bold text-white">
            {summary?.totalOrdersToday ?? orders.length}
          </p>
        </div>
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">In Kitchen</span>
          <p className="mt-2 text-2xl font-bold text-amber-300">
            {summary?.inKitchen ?? 0}
          </p>
        </div>
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">Ready for Pass</span>
          <p className="mt-2 text-2xl font-bold text-emerald-400">
            {summary?.readyForPickup ?? 0}
          </p>
        </div>
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">Delivered</span>
          <p className="mt-2 text-2xl font-bold text-blue-400">
            {summary?.deliveredToday ?? 0}
          </p>
        </div>
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">F&B Revenue</span>
          <p className="mt-2 text-2xl font-bold text-emerald-300">
            ${Number(summary?.totalRevenueToday || 0).toFixed(2)}
          </p>
        </div>
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <span className="text-xs text-[var(--text-secondary)]">Delayed (&gt;20m)</span>
          <p
            className={`mt-2 text-2xl font-bold ${
              (summary?.delayedOrders ?? 0) > 0 ? "text-rose-400" : "text-gray-400"
            }`}
          >
            {summary?.delayedOrders ?? 0}
          </p>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div className="flex items-center justify-between rounded-xl border border-red-400/30 bg-red-500/10 p-3 text-sm text-red-300">
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-4 w-4 shrink-0" />
            <span>{error}</span>
          </div>
          <button
            onClick={() => setError("")}
            className="text-xs text-red-400 underline hover:text-red-200"
          >
            Dismiss
          </button>
        </div>
      )}

      {successMsg && (
        <div className="flex items-center justify-between rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-3 text-sm text-emerald-300">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            <span>{successMsg}</span>
          </div>
          <button
            onClick={() => setSuccessMsg("")}
            className="text-xs text-emerald-400 underline hover:text-emerald-200"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Filters & Search Toolbar */}
      <div className="flex flex-col gap-3 rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by order #, room, or guest..."
              className="w-full rounded-xl border border-[var(--card-border)] bg-black/40 pl-9 pr-3 py-2 text-xs text-white placeholder-gray-500 focus:border-[#2F5C52] focus:outline-none"
            />
          </div>

          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] px-3 py-2 text-xs text-gray-200 focus:border-[#2F5C52] focus:outline-none"
          >
            <option value="ALL">All Statuses</option>
            <option value="Received">Received</option>
            <option value="Accepted">Accepted</option>
            <option value="Preparing">Preparing</option>
            <option value="Ready">Ready for Pass</option>
            <option value="Collected">Collected</option>
            <option value="Delivered">Delivered</option>
            <option value="Cancelled">Cancelled</option>
          </select>

          {/* Type Filter */}
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] px-3 py-2 text-xs text-gray-200 focus:border-[#2F5C52] focus:outline-none"
          >
            <option value="ALL">All Order Types</option>
            <option value="DineIn">Table Dine-In</option>
            <option value="RoomService">Room Service</option>
            <option value="Takeaway">Takeaway</option>
          </select>
        </div>

        {/* Delayed Toggle */}
        <label className="flex items-center gap-2 cursor-pointer select-none text-xs text-[var(--text-secondary)]">
          <input
            type="checkbox"
            checked={delayedOnly}
            onChange={(e) => setDelayedOnly(e.target.checked)}
            className="rounded border-[var(--card-border)] bg-transparent text-[#E87332] focus:ring-0"
          />
          <span>Delayed Only (&gt;20 min)</span>
        </label>
      </div>

      {/* Orders Table */}
      <div className="overflow-hidden rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-gray-300">
            <thead className="border-b border-[var(--card-border)] bg-black/40 text-[11px] font-semibold uppercase tracking-wider text-[var(--text-secondary)]">
              <tr>
                <th className="px-4 py-3.5">Order</th>
                <th className="px-4 py-3.5">Destination</th>
                <th className="px-4 py-3.5">Items Summary</th>
                <th className="px-4 py-3.5">Total & Finance</th>
                <th className="px-4 py-3.5">Kitchen Status</th>
                <th className="px-4 py-3.5">Staff</th>
                <th className="px-4 py-3.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--card-border)]/60">
              {filteredOrders.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-sm text-[var(--text-secondary)]">
                    No orders found matching the filter criteria.
                  </td>
                </tr>
              ) : (
                filteredOrders.map((order) => {
                  return (
                    <tr
                      key={order.id}
                      className="hover:bg-white/[0.02] transition"
                    >
                      {/* Order & Time */}
                      <td className="px-4 py-3">
                        <div className="font-mono font-bold text-white">
                          {order.orderNumber}
                        </div>
                        <div className="mt-0.5 text-[10px] text-[var(--text-secondary)]">
                          {new Date(order.receivedAt).toLocaleTimeString([], {
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                        </div>
                      </td>

                      {/* Destination / Type */}
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${
                            order.orderType === "RoomService"
                              ? "bg-purple-500/20 text-purple-300 border border-purple-500/30"
                              : order.orderType === "DineIn"
                              ? "bg-blue-500/20 text-blue-300 border border-blue-500/30"
                              : "bg-amber-500/20 text-amber-300 border border-amber-500/30"
                          }`}
                        >
                          {order.orderType === "RoomService" ? (
                            <>
                              <BedDouble className="h-3 w-3" />
                              Room {order.roomNumber || order.tableOrRoomNumber}
                            </>
                          ) : order.orderType === "DineIn" ? (
                            <>Table {order.tableOrRoomNumber}</>
                          ) : (
                            "Takeaway"
                          )}
                        </span>
                        {order.customerName && (
                          <div className="mt-1 text-[11px] text-gray-300">
                            {order.customerName}
                          </div>
                        )}
                      </td>

                      {/* Items */}
                      <td className="px-4 py-3 max-w-xs">
                        <div className="truncate font-medium text-gray-200">
                          {order.items && order.items.length > 0
                            ? order.items
                                .map((i) => `${i.quantity}x ${i.name}`)
                                .join(", ")
                            : "Items logged"}
                        </div>
                        {order.notes && (
                          <div className="mt-0.5 truncate text-[10px] text-amber-300/80">
                            Note: {order.notes}
                          </div>
                        )}
                      </td>

                      {/* Financials & Charge Status */}
                      <td className="px-4 py-3">
                        <div className="font-mono font-bold text-white">
                          ${Number(order.totalAmount || 0).toFixed(2)}
                        </div>
                        <div className="mt-1">
                          {order.chargeStatus === "Settled" ? (
                            <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                              <Receipt className="h-2.5 w-2.5" />
                              Inv #{order.financeInvoiceNumber || "OK"}
                            </span>
                          ) : order.chargeStatus === "PendingFinanceConfirmation" ? (
                            <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                              <RotateCw className="h-2.5 w-2.5 animate-spin" />
                              Pending Finance
                            </span>
                          ) : order.chargeStatus === "Failed" ? (
                            <div className="flex items-center gap-1">
                              <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30">
                                Charge Failed
                              </span>
                              <button
                                onClick={() => retryChargeMutation.mutate(order.id)}
                                disabled={retryChargeMutation.isPending}
                                className="rounded bg-rose-600/30 px-1.5 py-0.5 text-[10px] text-rose-200 hover:bg-rose-600/50"
                                title="Retry finance charge"
                              >
                                Retry
                              </button>
                            </div>
                          ) : (
                            <span className="text-[10px] text-[var(--text-secondary)]">
                              Not Delivered
                            </span>
                          )}
                        </div>
                      </td>

                      {/* Kitchen Status */}
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                            order.status === "Delivered"
                              ? "bg-blue-500/20 text-blue-300"
                              : order.status === "Ready"
                              ? "bg-emerald-500/20 text-emerald-300"
                              : order.status === "Preparing" || order.status === "Accepted"
                              ? "bg-orange-500/20 text-orange-300"
                              : order.status === "Cancelled"
                              ? "bg-rose-500/20 text-rose-300"
                              : "bg-gray-500/20 text-gray-300"
                          }`}
                        >
                          {order.status}
                        </span>
                      </td>

                      {/* Staff */}
                      <td className="px-4 py-3">
                        <div className="text-[11px] text-gray-300">
                          {order.waiterName ? `Waiter: ${order.waiterName}` : "—"}
                        </div>
                        <div className="text-[10px] text-[var(--text-secondary)]">
                          {order.chefName ? `Chef: ${order.chefName}` : ""}
                        </div>
                      </td>

                      {/* Actions */}
                      <td className="px-4 py-3 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {/* Waiter Collect */}
                          {order.status === "Ready" && (
                            <button
                              disabled={statusMutation.isPending}
                              onClick={() =>
                                statusMutation.mutate({
                                  orderId: order.id,
                                  status: "Collected",
                                })
                              }
                              className="rounded-lg bg-indigo-600 px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-indigo-500 transition"
                            >
                              Collect
                            </button>
                          )}

                          {/* Waiter Deliver */}
                          {order.status === "Collected" && (
                            <button
                              disabled={statusMutation.isPending}
                              onClick={() =>
                                statusMutation.mutate({
                                  orderId: order.id,
                                  status: "Delivered",
                                })
                              }
                              className="rounded-lg bg-[#2F5C52] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#254b42] transition"
                            >
                              Deliver
                            </button>
                          )}

                          {/* View Audit & Details */}
                          <button
                            onClick={() => setSelectedOrderForAudit(order)}
                            className="rounded-lg border border-[var(--card-border)] p-1 text-gray-400 hover:text-white transition"
                            title="View audit trail & item snapshot"
                          >
                            <Eye className="h-3.5 w-3.5" />
                          </button>

                          {/* Manager Cancel */}
                          {isManagerOrAdmin &&
                            order.status !== "Delivered" &&
                            order.status !== "Cancelled" && (
                              <button
                                onClick={() => setCancelModalOrder(order)}
                                className="rounded-lg border border-rose-500/30 p-1 text-rose-400 hover:bg-rose-500/10 transition"
                                title="Cancel order (requires reason)"
                              >
                                <XCircle className="h-3.5 w-3.5" />
                              </button>
                            )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* NEW ORDER MODAL */}
      {isNewOrderOpen && (
        <CreateOrderModal
          menuItems={menuItems}
          onClose={() => setIsNewOrderOpen(false)}
          onSuccess={() => {
            setIsNewOrderOpen(false);
            setSuccessMsg("Order placed successfully and routed to Kitchen Display.");
            refresh();
          }}
        />
      )}

      {/* AUDIT TRAIL & SNAPSHOT MODAL */}
      {selectedOrderForAudit && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
          <div className="w-full max-w-2xl rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-6 shadow-2xl overflow-y-auto max-h-[90vh]">
            <div className="flex items-center justify-between border-b border-[var(--card-border)] pb-4">
              <div>
                <h2 className="text-lg font-semibold text-white">
                  Order Details & Audit Trail: {selectedOrderForAudit.orderNumber}
                </h2>
                <p className="text-xs text-[var(--text-secondary)]">
                  Authoritative snapshot & immutable audit log
                </p>
              </div>
              <button
                onClick={() => setSelectedOrderForAudit(null)}
                className="text-gray-400 hover:text-white"
              >
                ✕
              </button>
            </div>

            {/* Snapshot info */}
            <div className="mt-4 space-y-3">
              <h3 className="text-xs font-semibold uppercase text-emerald-400 tracking-wide">
                Authoritative Items & Pricing Snapshot
              </h3>
              <div className="rounded-xl border border-[var(--card-border)] bg-black/30 p-3 space-y-2">
                {selectedOrderForAudit.items && selectedOrderForAudit.items.length > 0 ? (
                  selectedOrderForAudit.items.map((item, i) => (
                    <div key={i} className="flex justify-between items-center text-xs">
                      <div>
                        <span className="font-medium text-white">
                          {item.quantity}x {item.name}
                        </span>
                        <span className="ml-2 text-gray-400">
                          (@ ${Number(item.unitPrice).toFixed(2)} + {(item.taxRate * 100).toFixed(1)}% tax)
                        </span>
                        {item.specialInstructions && (
                          <div className="text-[10px] text-amber-300 italic">
                            Instruction: {item.specialInstructions}
                          </div>
                        )}
                      </div>
                      <span className="font-mono text-white">
                        ${Number(item.lineTotal).toFixed(2)}
                      </span>
                    </div>
                  ))
                ) : (
                  <p className="text-xs text-gray-400 italic">No structured items snapshot</p>
                )}
                <div className="border-t border-[var(--card-border)] pt-2 flex justify-between text-xs font-bold text-white">
                  <span>Subtotal: ${Number(selectedOrderForAudit.subtotal || 0).toFixed(2)}</span>
                  <span>Tax: ${Number(selectedOrderForAudit.taxAmount || 0).toFixed(2)}</span>
                  <span>Total: ${Number(selectedOrderForAudit.totalAmount || 0).toFixed(2)}</span>
                </div>
              </div>

              {/* Finance settlement state */}
              <div className="rounded-xl border border-[var(--card-border)] bg-black/30 p-3 text-xs space-y-1">
                <div className="flex justify-between">
                  <span className="text-gray-400">Finance Status:</span>
                  <span className="font-semibold text-white">{selectedOrderForAudit.chargeStatus}</span>
                </div>
                {selectedOrderForAudit.financeInvoiceNumber && (
                  <div className="flex justify-between">
                    <span className="text-gray-400">Invoice Number:</span>
                    <span className="font-mono text-emerald-300">{selectedOrderForAudit.financeInvoiceNumber}</span>
                  </div>
                )}
                {selectedOrderForAudit.chargeError && (
                  <div className="text-rose-400 text-[11px] mt-1">
                    Charge Error: {selectedOrderForAudit.chargeError}
                  </div>
                )}
              </div>

              {/* Audit logs */}
              <h3 className="text-xs font-semibold uppercase text-emerald-400 tracking-wide mt-4">
                Immutable Audit Logs
              </h3>
              <div className="rounded-xl border border-[var(--card-border)] bg-black/30 p-3 space-y-2 max-h-56 overflow-y-auto">
                {auditLogsQuery.data && auditLogsQuery.data.length > 0 ? (
                  auditLogsQuery.data.map((log: FnbAuditLogDto) => (
                    <div key={log.id} className="border-b border-white/5 pb-2 text-xs">
                      <div className="flex justify-between text-gray-300">
                        <span className="font-semibold text-white">{log.action}</span>
                        <span className="font-mono text-[10px] text-gray-400">
                          {new Date(log.timestamp).toLocaleString()}
                        </span>
                      </div>
                      <div className="text-[11px] text-[var(--text-secondary)] mt-0.5">
                        Actor: {log.actorRole || "System"} · {log.details}
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="text-xs text-gray-400 italic">No audit logs recorded.</p>
                )}
              </div>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                onClick={() => setSelectedOrderForAudit(null)}
                className="rounded-xl bg-[#2F5C52] px-4 py-2 text-xs font-semibold text-white hover:bg-[#254b42]"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* CANCELLATION MODAL */}
      {cancelModalOrder && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
          <div className="w-full max-w-md rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-6 shadow-2xl">
            <div className="flex items-center gap-3 text-rose-400">
              <XCircle className="h-6 w-6" />
              <h2 className="text-lg font-semibold text-white">
                Cancel Order {cancelModalOrder.orderNumber}
              </h2>
            </div>
            <p className="mt-2 text-xs text-[var(--text-secondary)]">
              An immutable audit log will be created. A minimum 5-character reason is required by
              hotel governance policies.
            </p>
            <div className="mt-4">
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                Reason for cancellation *
              </label>
              <textarea
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="e.g. Guest canceled, ingredient unavailable..."
                className="mt-1 min-h-24 w-full rounded-xl border border-[var(--card-border)] bg-black/40 p-3 text-sm text-white placeholder-gray-500 focus:border-[#E87332] focus:outline-none"
              />
            </div>
            <div className="mt-5 flex justify-end gap-3">
              <button
                onClick={() => {
                  setCancelModalOrder(null);
                  setCancelReason("");
                }}
                className="rounded-xl border border-[var(--card-border)] px-4 py-2 text-sm text-gray-300 hover:bg-white/5"
              >
                Keep Order
              </button>
              <button
                disabled={cancelMutation.isPending || cancelReason.trim().length < 5}
                onClick={() =>
                  cancelMutation.mutate({
                    orderId: cancelModalOrder.id,
                    reason: cancelReason.trim(),
                  })
                }
                className="rounded-xl bg-rose-600 px-4 py-2 text-sm font-medium text-white hover:bg-rose-500 disabled:opacity-40 transition"
              >
                {cancelMutation.isPending ? "Cancelling..." : "Confirm Cancellation"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// -----------------------------------------------------------------------------
// CREATE ORDER MODAL COMPONENT
// -----------------------------------------------------------------------------

interface CreateOrderModalProps {
  menuItems: MenuItemDto[];
  onClose: () => void;
  onSuccess: () => void;
}

type CartItem = { menuItem: MenuItemDto; quantity: number; instructions: string };

function CreateOrderModal({ menuItems, onClose, onSuccess }: CreateOrderModalProps) {
  const [orderType, setOrderType] = useState<OrderType>("DineIn");
  const [tableOrRoom, setTableOrRoom] = useState("");
  const [orderNotes, setOrderNotes] = useState("");
  const [cart, setCart] = useState<CartItem[]>([]);
  const [modalError, setModalError] = useState("");

  const subtotal = useMemo(() => {
    return cart.reduce((sum, item) => sum + item.menuItem.price * item.quantity, 0);
  }, [cart]);

  const addItemToCart = (item: MenuItemDto) => {
    setCart((prev) => {
      const existing = prev.find((c) => c.menuItem.id === item.id);
      if (existing) {
        return prev.map((c) =>
          c.menuItem.id === item.id ? { ...c, quantity: c.quantity + 1 } : c
        );
      }
      return [...prev, { menuItem: item, quantity: 1, instructions: "" }];
    });
  };

  const updateQuantity = (itemId: string, delta: number) => {
    setCart((prev) =>
      prev
        .map((c) => {
          if (c.menuItem.id === itemId) {
            const newQty = c.quantity + delta;
            return newQty > 0 ? { ...c, quantity: newQty } : null;
          }
          return c;
        })
        .filter((item): item is CartItem => item !== null)
    );
  };

  const updateInstructions = (itemId: string, instructions: string) => {
    setCart((prev) =>
      prev.map((c) => (c.menuItem.id === itemId ? { ...c, instructions } : c))
    );
  };

  const createOrderMutation = useMutation({
    mutationFn: async () => {
      setModalError("");
      if (!tableOrRoom.trim()) {
        throw new Error(
          orderType === "RoomService"
            ? "Room number is required for room service"
            : "Table number is required"
        );
      }
      if (cart.length === 0) {
        throw new Error("Select at least one menu item");
      }

      const items: CreateStructuredOrderItem[] = cart.map((c) => ({
        menuItemId: c.menuItem.id,
        quantity: c.quantity,
        specialInstructions: c.instructions || undefined,
      }));

      return await fnbApi.createOrder({
        orderType,
        tableOrRoomNumber: tableOrRoom.trim(),
        roomNumber: orderType === "RoomService" ? tableOrRoom.trim() : undefined,
        notes: orderNotes.trim() || undefined,
        items,
      });
    },
    onSuccess: () => {
      onSuccess();
    },
    onError: (err: unknown) => setModalError(getApiErrorMessage(err, "Order creation failed")),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-4">
      <div className="flex flex-col w-full max-w-3xl rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] shadow-2xl overflow-hidden max-h-[90vh]">
        {/* Modal Header */}
        <div className="flex items-center justify-between border-b border-[var(--card-border)] px-6 py-4">
          <div>
            <h2 className="text-lg font-semibold text-white">Create Food & Beverage Order</h2>
            <p className="text-xs text-[var(--text-secondary)]">
              Select items from authoritative menu, verify guest eligibility, and route to kitchen
            </p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-white">
            ✕
          </button>
        </div>

        {/* Modal Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-5">
          {modalError && (
            <div className="rounded-xl border border-red-400/30 bg-red-500/10 p-3 text-xs text-red-300 flex items-center gap-2">
              <ShieldAlert className="h-4 w-4 shrink-0" />
              <span>{modalError}</span>
            </div>
          )}

          {/* Order Type & Destination */}
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                Order Type *
              </label>
              <div className="mt-1.5 flex gap-2">
                {(["DineIn", "RoomService", "Takeaway"] as OrderType[]).map((type) => (
                  <button
                    key={type}
                    type="button"
                    onClick={() => setOrderType(type)}
                    className={`flex-1 rounded-xl border py-2 text-xs font-medium transition ${
                      orderType === type
                        ? "border-[#2F5C52] bg-[#2F5C52] text-white"
                        : "border-[var(--card-border)] bg-black/20 text-gray-400 hover:text-white"
                    }`}
                  >
                    {type === "DineIn"
                      ? "Table"
                      : type === "RoomService"
                      ? "Room Service"
                      : "Takeaway"}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                {orderType === "RoomService"
                  ? "Room Number *"
                  : orderType === "DineIn"
                  ? "Table Number *"
                  : "Pickup Reference *"}
              </label>
              <input
                type="text"
                value={tableOrRoom}
                onChange={(e) => setTableOrRoom(e.target.value)}
                placeholder={
                  orderType === "RoomService"
                    ? "e.g. 304 (must be active checked-in guest)"
                    : orderType === "DineIn"
                    ? "e.g. 12"
                    : "e.g. Takeaway-01"
                }
                className="mt-1.5 w-full rounded-xl border border-[var(--card-border)] bg-black/40 px-3 py-2 text-xs text-white placeholder-gray-500 focus:border-[#2F5C52] focus:outline-none"
              />
              {orderType === "RoomService" && (
                <p className="mt-1 text-[10px] text-amber-300/80">
                  Room service orders are validated in real time against active bookings. Fail-closed
                  governance applies.
                </p>
              )}
            </div>
          </div>

          {/* Menu Catalog Picker */}
          <div>
            <label className="text-xs font-medium text-[var(--text-secondary)]">
              Select Menu Items
            </label>
            <div className="mt-2 grid gap-2 sm:grid-cols-2 max-h-48 overflow-y-auto p-1 border border-[var(--card-border)]/60 rounded-xl bg-black/20">
              {menuItems
                .filter((item) => item.available)
                .map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => addItemToCart(item)}
                    className="flex items-center justify-between rounded-lg border border-[var(--card-border)]/40 bg-[var(--card-dark)] p-2.5 text-left hover:border-emerald-500/50 transition"
                  >
                    <div>
                      <div className="text-xs font-medium text-white">{item.name}</div>
                      <div className="text-[10px] text-gray-400">{item.category}</div>
                    </div>
                    <div className="text-right">
                      <div className="font-mono text-xs font-bold text-emerald-400">
                        ${Number(item.price).toFixed(2)}
                      </div>
                      <span className="text-[10px] text-[#2F5C52] font-semibold">+ Add</span>
                    </div>
                  </button>
                ))}
            </div>
          </div>

          {/* Selected Order Cart */}
          <div>
            <label className="text-xs font-medium text-[var(--text-secondary)]">
              Order Items ({cart.length})
            </label>
            <div className="mt-2 space-y-2">
              {cart.length === 0 ? (
                <div className="rounded-xl border border-[var(--card-border)] bg-black/10 p-4 text-center text-xs text-gray-500">
                  No items selected yet. Click a menu item above to add it.
                </div>
              ) : (
                cart.map((c) => (
                  <div
                    key={c.menuItem.id}
                    className="rounded-xl border border-[var(--card-border)] bg-black/30 p-3 space-y-2"
                  >
                    <div className="flex items-center justify-between">
                      <div>
                        <span className="text-xs font-semibold text-white">
                          {c.menuItem.name}
                        </span>
                        <span className="ml-2 font-mono text-xs text-gray-400">
                          ${Number(c.menuItem.price).toFixed(2)} ea
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={() => updateQuantity(c.menuItem.id, -1)}
                          className="h-6 w-6 rounded bg-white/10 text-xs font-bold text-white hover:bg-white/20"
                        >
                          -
                        </button>
                        <span className="font-mono text-xs font-bold text-white">
                          {c.quantity}
                        </span>
                        <button
                          type="button"
                          onClick={() => updateQuantity(c.menuItem.id, 1)}
                          className="h-6 w-6 rounded bg-white/10 text-xs font-bold text-white hover:bg-white/20"
                        >
                          +
                        </button>
                        <span className="ml-2 font-mono text-xs font-bold text-emerald-400">
                          ${(c.menuItem.price * c.quantity).toFixed(2)}
                        </span>
                      </div>
                    </div>
                    <input
                      type="text"
                      value={c.instructions}
                      onChange={(e) => updateInstructions(c.menuItem.id, e.target.value)}
                      placeholder="Special instructions (e.g. no onions, extra dressing)..."
                      className="w-full rounded-lg border border-[var(--card-border)]/60 bg-black/20 px-2 py-1 text-[11px] text-gray-300 placeholder-gray-500 focus:outline-none"
                    />
                  </div>
                ))
              )}
            </div>
          </div>

          {/* General Notes */}
          <div>
            <label className="text-xs font-medium text-[var(--text-secondary)]">
              Order Notes / Delivery Instructions
            </label>
            <input
              type="text"
              value={orderNotes}
              onChange={(e) => setOrderNotes(e.target.value)}
              placeholder="e.g. Deliver immediately after 8:00 PM..."
              className="mt-1.5 w-full rounded-xl border border-[var(--card-border)] bg-black/40 px-3 py-2 text-xs text-white placeholder-gray-500 focus:border-[#2F5C52] focus:outline-none"
            />
          </div>

          {/* Price Preview */}
          <div className="rounded-xl border border-[var(--card-border)] bg-black/40 p-3 flex justify-between items-center text-xs">
            <span className="text-[var(--text-secondary)]">
              Subtotal (Authoritative taxes calculated server-side upon placement):
            </span>
            <span className="font-mono text-sm font-bold text-emerald-400">
              ${subtotal.toFixed(2)}
            </span>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="flex items-center justify-end gap-3 border-t border-[var(--card-border)] px-6 py-4 bg-black/20">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-[var(--card-border)] px-4 py-2 text-xs text-gray-300 hover:bg-white/5"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={createOrderMutation.isPending || cart.length === 0}
            onClick={() => createOrderMutation.mutate()}
            className="rounded-xl bg-[#2F5C52] px-5 py-2 text-xs font-semibold text-white hover:bg-[#254b42] disabled:opacity-40 transition"
          >
            {createOrderMutation.isPending ? "Submitting..." : "Send to Kitchen"}
          </button>
        </div>
      </div>
    </div>
  );
}
