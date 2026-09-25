"use client";

import { useAuthStore } from "@/features/auth/store/useAuthStore";
import { getApiErrorMessage } from "@/lib/apiError";
import { fnbApi,type KdsOrderResponse,type KdsStatus } from "@/services/fnbApi";
import { useMutation,useQuery,useQueryClient } from "@tanstack/react-query";
import {
AlertTriangle,
Check,
CheckCircle2,
ChefHat,
Clock3,
Flame,
Play,
RefreshCw,
Send,
User,
UtensilsCrossed,
XCircle
} from "lucide-react";
import { useMemo,useState } from "react";

function getElapsedMinutes(dateStr: string): number {
  if (!dateStr) return 0;
  const diff = Date.now() - new Date(dateStr).getTime();
  return Math.max(0, Math.floor(diff / 60000));
}

export default function KdsPage() {
  const user = useAuthStore((s) => s.user);
  const userRole = String(user?.role || "");
  const isChef = userRole === "Chef" || userRole === "Kitchen";
  const isWaiter = userRole === "Waiter";
  const isManagerOrAdmin = ["Manager", "Admin", "Owner"].includes(userRole);

  const qc = useQueryClient();
  const [error, setError] = useState("");
  const [cancelModalOrder, setCancelModalOrder] = useState<KdsOrderResponse | null>(null);
  const [cancelReason, setCancelReason] = useState("");

  // Poll kitchen queue every 10 seconds
  const ordersQuery = useQuery({
    queryKey: ["kds-kitchen-queue"],
    queryFn: () => fnbApi.getKitchenQueue(),
    refetchInterval: 10_000,
  });

  const orders = useMemo(() => ordersQuery.data ?? [], [ordersQuery.data]);

  const refresh = () => qc.invalidateQueries({ queryKey: ["kds-kitchen-queue"] });

  // Transition mutation
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
      return await fnbApi.updateStatus(orderId, { status, notes });
    },
    onSuccess: () => {
      refresh();
    },
    onError: (err: unknown) => setError(getApiErrorMessage(err, "Status update failed")),
  });

  // Cancel mutation
  const cancelMutation = useMutation({
    mutationFn: async ({ orderId, reason }: { orderId: string; reason: string }) => {
      setError("");
      return await fnbApi.cancelOrder(orderId, reason);
    },
    onSuccess: () => {
      setCancelModalOrder(null);
      setCancelReason("");
      refresh();
    },
    onError: (err: unknown) => setError(getApiErrorMessage(err, "Cancellation failed")),
  });

  // Lanes categorization
  const incomingOrders = useMemo(
    () => orders.filter((o) => o.status === "Received"),
    [orders]
  );
  const prepOrders = useMemo(
    () => orders.filter((o) => o.status === "Accepted" || o.status === "Preparing"),
    [orders]
  );
  const hotPassOrders = useMemo(
    () => orders.filter((o) => o.status === "Ready" || o.status === "Collected"),
    [orders]
  );

  const delayedCount = useMemo(
    () => orders.filter((o) => getElapsedMinutes(o.receivedAt) >= 20).length,
    [orders]
  );

  return (
    <div className="mx-auto max-w-7xl space-y-6 p-6 lg:p-8">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#E87332]/20 text-[#E87332]">
              <ChefHat className="h-6 w-6" />
            </div>
            <div>
              <h1
                className="text-3xl font-bold tracking-tight text-white"
                style={{ fontFamily: "var(--font-fraunces), serif" }}
              >
                Kitchen Display System
              </h1>
              <p className="mt-1 text-sm text-[var(--text-secondary)]">
                Real-time kitchen order sequencing, prep staging, and hot pass delivery
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] px-3 py-1.5 text-xs text-[var(--text-secondary)]">
            <span className="h-2 w-2 rounded-full bg-emerald-400 animate-pulse" />
            Live Sync (10s)
          </div>
          <button
            onClick={() => refresh()}
            disabled={ordersQuery.isFetching}
            className="flex items-center gap-2 rounded-xl border border-[var(--card-border)] bg-[var(--card-dark)] px-3 py-2 text-sm text-[var(--text-secondary)] hover:text-white transition disabled:opacity-50"
            title="Refresh order queue"
          >
            <RefreshCw
              className={`h-4 w-4 ${ordersQuery.isFetching ? "animate-spin text-[#E87332]" : ""}`}
            />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* Metrics Bar */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-[var(--text-secondary)] uppercase tracking-wider">
              Incoming Queue
            </span>
            <Clock3 className="h-5 w-5 text-amber-400" />
          </div>
          <p className="mt-2 text-2xl font-bold text-white">{incomingOrders.length}</p>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">Awaiting kitchen acceptance</p>
        </div>

        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-[var(--text-secondary)] uppercase tracking-wider">
              In Preparation
            </span>
            <Flame className="h-5 w-5 text-[#E87332]" />
          </div>
          <p className="mt-2 text-2xl font-bold text-white">{prepOrders.length}</p>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">Cooking on stations</p>
        </div>

        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-[var(--text-secondary)] uppercase tracking-wider">
              Hot Pass Ready
            </span>
            <CheckCircle2 className="h-5 w-5 text-emerald-400" />
          </div>
          <p className="mt-2 text-2xl font-bold text-white">{hotPassOrders.length}</p>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">Ready for waitstaff pickup</p>
        </div>

        <div className="rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-[var(--text-secondary)] uppercase tracking-wider">
              Delayed Tickets
            </span>
            <AlertTriangle className={`h-5 w-5 ${delayedCount > 0 ? "text-rose-400" : "text-gray-500"}`} />
          </div>
          <p className={`mt-2 text-2xl font-bold ${delayedCount > 0 ? "text-rose-400" : "text-white"}`}>
            {delayedCount}
          </p>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">&gt; 20 minutes elapsed</p>
        </div>
      </div>

      {/* Error alert */}
      {error && (
        <div className="flex items-center justify-between rounded-xl border border-red-400/30 bg-red-500/10 p-3 text-sm text-red-300">
          <span>{error}</span>
          <button
            onClick={() => setError("")}
            className="text-xs text-red-400 underline hover:text-red-200"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* 3 KDS Staging Lanes */}
      <div className="grid gap-4 lg:grid-cols-3">
        {/* Lane 1: Incoming Queue */}
        <section className="flex flex-col rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]/70">
          <div className="flex items-center justify-between border-b border-[var(--card-border)] px-4 py-3 bg-amber-500/10 rounded-t-2xl">
            <div className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 rounded-full bg-amber-400" />
              <h2 className="text-sm font-semibold text-amber-200 uppercase tracking-wide">
                Incoming Queue ({incomingOrders.length})
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-3 p-3 overflow-y-auto max-h-[720px]">
            {incomingOrders.length === 0 ? (
              <div className="py-12 text-center text-xs text-[var(--text-secondary)]">
                No orders waiting in queue
              </div>
            ) : (
              incomingOrders.map((order) => (
                <TicketCard
                  key={order.id}
                  order={order}
                  isChef={isChef}
                  isWaiter={isWaiter}
                  isManager={isManagerOrAdmin}
                  onStatusUpdate={(status) =>
                    statusMutation.mutate({ orderId: order.id, status })
                  }
                  onCancel={() => setCancelModalOrder(order)}
                  isPending={statusMutation.isPending}
                />
              ))
            )}
          </div>
        </section>

        {/* Lane 2: In Preparation */}
        <section className="flex flex-col rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]/70">
          <div className="flex items-center justify-between border-b border-[var(--card-border)] px-4 py-3 bg-[#E87332]/10 rounded-t-2xl">
            <div className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 rounded-full bg-[#E87332]" />
              <h2 className="text-sm font-semibold text-orange-200 uppercase tracking-wide">
                In Preparation ({prepOrders.length})
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-3 p-3 overflow-y-auto max-h-[720px]">
            {prepOrders.length === 0 ? (
              <div className="py-12 text-center text-xs text-[var(--text-secondary)]">
                No tickets currently in prep
              </div>
            ) : (
              prepOrders.map((order) => (
                <TicketCard
                  key={order.id}
                  order={order}
                  isChef={isChef}
                  isWaiter={isWaiter}
                  isManager={isManagerOrAdmin}
                  onStatusUpdate={(status) =>
                    statusMutation.mutate({ orderId: order.id, status })
                  }
                  onCancel={() => setCancelModalOrder(order)}
                  isPending={statusMutation.isPending}
                />
              ))
            )}
          </div>
        </section>

        {/* Lane 3: Hot Pass Ready */}
        <section className="flex flex-col rounded-2xl border border-[var(--card-border)] bg-[var(--card-dark)]/70">
          <div className="flex items-center justify-between border-b border-[var(--card-border)] px-4 py-3 bg-emerald-500/10 rounded-t-2xl">
            <div className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 rounded-full bg-emerald-400" />
              <h2 className="text-sm font-semibold text-emerald-200 uppercase tracking-wide">
                Hot Pass Ready ({hotPassOrders.length})
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-3 p-3 overflow-y-auto max-h-[720px]">
            {hotPassOrders.length === 0 ? (
              <div className="py-12 text-center text-xs text-[var(--text-secondary)]">
                Hot pass is clear
              </div>
            ) : (
              hotPassOrders.map((order) => (
                <TicketCard
                  key={order.id}
                  order={order}
                  isChef={isChef}
                  isWaiter={isWaiter}
                  isManager={isManagerOrAdmin}
                  onStatusUpdate={(status) =>
                    statusMutation.mutate({ orderId: order.id, status })
                  }
                  onCancel={() => setCancelModalOrder(order)}
                  isPending={statusMutation.isPending}
                />
              ))
            )}
          </div>
        </section>
      </div>

      {/* Cancellation Modal (Requires mandatory reason) */}
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
              An immutable audit log will be created. A minimum 5-character reason is required
              by hotel governance policies.
            </p>
            <div className="mt-4">
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                Reason for cancellation *
              </label>
              <textarea
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="e.g. Guest changed mind, kitchen out of key ingredient..."
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
// Ticket Card Component
// -----------------------------------------------------------------------------

interface TicketCardProps {
  order: KdsOrderResponse;
  isChef: boolean;
  isWaiter: boolean;
  isManager: boolean;
  onStatusUpdate: (status: KdsStatus) => void;
  onCancel: () => void;
  isPending: boolean;
}

function TicketCard({
  order,
  isChef,
  isWaiter,
  isManager,
  onStatusUpdate,
  onCancel,
  isPending,
}: TicketCardProps) {
  const elapsed = getElapsedMinutes(order.receivedAt);
  const isDelayed = elapsed >= 20;

  return (
    <article
      className={`rounded-xl border p-4 transition-all duration-200 ${
        isDelayed
          ? "border-rose-500/60 bg-rose-950/10 shadow-lg shadow-rose-950/20"
          : "border-[var(--card-border)] bg-[var(--card-dark)]"
      }`}
    >
      {/* Header Bar */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm font-bold text-white">
              {order.orderNumber}
            </span>
            <span
              className={`rounded-md px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${
                order.orderType === "RoomService"
                  ? "bg-purple-500/20 text-purple-300 border border-purple-500/30"
                  : order.orderType === "DineIn"
                  ? "bg-blue-500/20 text-blue-300 border border-blue-500/30"
                  : "bg-amber-500/20 text-amber-300 border border-amber-500/30"
              }`}
            >
              {order.orderType === "RoomService"
                ? `Room ${order.roomNumber || order.tableOrRoomNumber}`
                : order.orderType === "DineIn"
                ? `Table ${order.tableOrRoomNumber}`
                : "Takeaway"}
            </span>
          </div>
          {order.customerName && (
            <p className="mt-1 flex items-center gap-1 text-xs text-[var(--text-secondary)]">
              <User className="h-3 w-3" />
              <span>{order.customerName}</span>
            </p>
          )}
        </div>

        {/* Elapsed Timer Badge */}
        <div
          className={`flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-medium font-mono ${
            isDelayed
              ? "bg-rose-500/20 text-rose-300 border border-rose-500/30 animate-pulse"
              : "bg-white/5 text-[var(--text-secondary)]"
          }`}
        >
          <Clock3 className="h-3.5 w-3.5" />
          <span>{elapsed}m</span>
        </div>
      </div>

      {/* Items Section */}
      <div className="mt-3 border-t border-[var(--card-border)]/60 pt-2 space-y-2">
        {order.items && order.items.length > 0 ? (
          order.items.map((item, idx) => (
            <div key={idx} className="flex flex-col text-xs">
              <div className="flex justify-between items-center text-gray-200">
                <span className="font-medium">
                  <span className="text-[#E87332] font-bold mr-1.5">{item.quantity}x</span>
                  {item.name}
                </span>
                <span className="text-gray-400 font-mono">
                  ${Number(item.lineTotal || 0).toFixed(2)}
                </span>
              </div>
              {item.specialInstructions && (
                <span className="mt-0.5 inline-block text-[11px] text-amber-300/90 italic bg-amber-500/10 px-1.5 py-0.5 rounded">
                  Note: {item.specialInstructions}
                </span>
              )}
            </div>
          ))
        ) : (
          <p className="text-xs text-gray-400 italic">Structured items parsed</p>
        )}
      </div>

      {/* Order Notes */}
      {order.notes && (
        <div className="mt-2.5 rounded-lg bg-white/5 p-2 text-[11px] text-[var(--text-secondary)]">
          <span className="font-medium text-gray-300">Order Note:</span> {order.notes}
        </div>
      )}

      {/* Staff assignments */}
      {(order.chefName || order.waiterName) && (
        <div className="mt-2.5 flex items-center gap-3 text-[11px] text-[var(--text-secondary)]">
          {order.chefName && (
            <span className="flex items-center gap-1">
              <ChefHat className="h-3 w-3 text-[#E87332]" />
              {order.chefName}
            </span>
          )}
          {order.waiterName && (
            <span className="flex items-center gap-1">
              <UtensilsCrossed className="h-3 w-3 text-emerald-400" />
              {order.waiterName}
            </span>
          )}
        </div>
      )}

      {/* Dynamic Action Buttons */}
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-[var(--card-border)]/60 pt-3">
        {/* Chef / Kitchen Actions */}
        {(isChef || isManager) && order.status === "Received" && (
          <button
            disabled={isPending}
            onClick={() => onStatusUpdate("Accepted")}
            className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-[#2F5C52] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#254b42] transition disabled:opacity-50"
          >
            <Check className="h-3.5 w-3.5" />
            <span>Accept Order</span>
          </button>
        )}

        {(isChef || isManager) && order.status === "Accepted" && (
          <button
            disabled={isPending}
            onClick={() => onStatusUpdate("Preparing")}
            className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-[#E87332] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#cf6327] transition disabled:opacity-50"
          >
            <Play className="h-3.5 w-3.5" />
            <span>Start Cooking</span>
          </button>
        )}

        {(isChef || isManager) && order.status === "Preparing" && (
          <button
            disabled={isPending}
            onClick={() => onStatusUpdate("Ready")}
            className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-500 transition disabled:opacity-50"
          >
            <CheckCircle2 className="h-3.5 w-3.5" />
            <span>Ready for Pass</span>
          </button>
        )}

        {/* Waiter Actions */}
        {(isWaiter || isManager) && order.status === "Ready" && (
          <button
            disabled={isPending}
            onClick={() => onStatusUpdate("Collected")}
            className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-500 transition disabled:opacity-50"
          >
            <UtensilsCrossed className="h-3.5 w-3.5" />
            <span>Collect from Pass</span>
          </button>
        )}

        {(isWaiter || isManager) && order.status === "Collected" && (
          <button
            disabled={isPending}
            onClick={() => onStatusUpdate("Delivered")}
            className="flex-1 flex items-center justify-center gap-1.5 rounded-lg bg-[#2F5C52] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#254b42] transition disabled:opacity-50"
          >
            <Send className="h-3.5 w-3.5" />
            <span>Confirm Delivered</span>
          </button>
        )}

        {/* Manager Override Cancel Button */}
        {isManager && (
          <button
            disabled={isPending}
            onClick={onCancel}
            className="rounded-lg border border-rose-500/30 px-2 py-1.5 text-xs text-rose-400 hover:bg-rose-500/10 transition disabled:opacity-50"
            title="Cancel order (requires reason)"
          >
            Cancel
          </button>
        )}
      </div>
    </article>
  );
}
