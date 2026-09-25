import api from "@/lib/axios";

export type KdsStatus =
  | "Received"
  | "Accepted"
  | "Preparing"
  | "Ready"
  | "Collected"
  | "Delivered"
  | "Cancelled";

export type OrderType = "DineIn" | "RoomService" | "Takeaway";

export type ChargeStatus =
  | "None"
  | "PendingFinanceConfirmation"
  | "Settled"
  | "Failed"
  | "Reconciled";

export interface MenuItemDto {
  id: string;
  code: string;
  name: string;
  description: string;
  category: string;
  price: number;
  currency: string;
  available: boolean;
  roomServiceEligible: boolean;
  imageUrl?: string;
}

export interface OrderItemSnapshot {
  menuItemId?: string;
  code?: string;
  name: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  taxAmount: number;
  lineSubtotal: number;
  lineTotal: number;
  specialInstructions?: string;
}

export interface KdsOrderResponse {
  id: string;
  orderNumber: string;
  orderType: OrderType;
  tableOrRoomNumber: string;
  roomNumber?: string;
  bookingId?: string;
  customerId?: string;
  customerName?: string;
  itemsJson: string;
  items: OrderItemSnapshot[];
  status: KdsStatus;
  cookEmployeeId?: string;
  chefEmployeeId?: string;
  chefName?: string;
  waiterEmployeeId?: string;
  waiterName?: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;
  financeInvoiceId?: string;
  financeInvoiceNumber?: string;
  chargeStatus: ChargeStatus;
  chargeError?: string;
  notes?: string;
  cancellationReason?: string;
  receivedAt: string;
  acceptedAt?: string;
  preparingAt?: string;
  readyAt?: string;
  collectedAt?: string;
  deliveredAt?: string;
  cancelledAt?: string;
}

export interface CreateStructuredOrderItem {
  menuItemId: string;
  quantity: number;
  specialInstructions?: string;
}

export interface CreateStructuredOrderRequest {
  orderType: OrderType;
  tableOrRoomNumber: string;
  roomNumber?: string;
  bookingId?: string;
  notes?: string;
  items: CreateStructuredOrderItem[];
}

export interface UpdateKdsStatusRequest {
  status: KdsStatus;
  cookEmployeeId?: string;
  notes?: string;
}

export interface CancelOrderRequest {
  reason: string;
}

export interface AssignOrderRequest {
  employeeId: string;
  role: string;
}

export interface FinanceChargeResult {
  success: boolean;
  invoiceId?: string;
  invoiceNumber?: string;
  errorCode?: string;
  errorMessage?: string;
  requiresReconciliation: boolean;
}

export interface FnbSummaryResponse {
  totalOrdersToday: number;
  inKitchen: number;
  readyForPickup: number;
  deliveredToday: number;
  cancelledToday: number;
  delayedOrders: number;
  totalRevenueToday: number;
  pendingFinanceCharges: number;
}

export interface FnbAuditLogDto {
  id: string;
  orderId: string;
  action: string;
  fromStatus?: string;
  toStatus?: string;
  actorId?: string;
  actorRole?: string;
  details?: string;
  timestamp: string;
}

const root = "/api/v1/kds";

export const fnbApi = {
  getMenu: async () => (await api.get<MenuItemDto[]>(`${root}/menu`)).data,

  getActiveOrders: async () => (await api.get<KdsOrderResponse[]>(`${root}/orders/active`)).data,

  getKitchenQueue: async () => (await api.get<KdsOrderResponse[]>(`${root}/orders/kitchen`)).data,

  getOrders: async (params?: {
    status?: KdsStatus;
    orderType?: OrderType;
    delayedOnly?: boolean;
  }) => {
    const query = new URLSearchParams();
    if (params?.status) query.set("status", params.status);
    if (params?.orderType) query.set("orderType", params.orderType);
    if (params?.delayedOnly) query.set("delayedOnly", "true");
    const qs = query.toString();
    return (await api.get<KdsOrderResponse[]>(`${root}/orders${qs ? `?${qs}` : ""}`)).data;
  },

  getOrderById: async (id: string) => (await api.get<KdsOrderResponse>(`${root}/orders/${id}`)).data,

  getAuditLogs: async (id: string) => (await api.get<FnbAuditLogDto[]>(`${root}/orders/${id}/audit-logs`)).data,

  createOrder: async (req: CreateStructuredOrderRequest) =>
    (await api.post<KdsOrderResponse>(`${root}/orders`, req)).data,

  updateStatus: async (id: string, req: UpdateKdsStatusRequest) =>
    (await api.patch<KdsOrderResponse>(`${root}/orders/${id}/status`, req)).data,

  cancelOrder: async (id: string, reason: string) =>
    (await api.post<KdsOrderResponse>(`${root}/orders/${id}/cancel`, { reason })).data,

  assignOrder: async (id: string, employeeId: string, role: string) =>
    (await api.post<KdsOrderResponse>(`${root}/orders/${id}/assign`, { employeeId, role })).data,

  retryFinanceCharge: async (id: string) =>
    (await api.post<FinanceChargeResult>(`${root}/orders/${id}/retry-charge`)).data,

  getSummary: async () => (await api.get<FnbSummaryResponse>(`${root}/reports/summary`)).data,
};
