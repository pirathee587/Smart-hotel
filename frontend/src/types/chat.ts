// ── Chat Domain Types ──────────────────────────────────────────────────────────

export type ChatRole = 'System' | 'User' | 'Assistant' | 'Tool';
export type ChatSessionStatus = 'Active' | 'Escalated' | 'Closed';
export type RequestStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled';
export type TaskStatus = 'Pending' | 'Assigned' | 'Accepted' | 'InProgress' | 'Completed' | 'Cancelled';
export type Priority = 'Low' | 'Medium' | 'High' | 'Urgent';

export interface ChatMessageDto {
  id: string;
  role: ChatRole;
  content: string;
  createdAt: string;
  isRedacted: boolean;
}

export interface ChatSession {
  id: string;
  customerId: string;
  bookingId: string;
  startedAt: string;
  lastMessageAt: string;
  status: ChatSessionStatus;
}

// ── Send Message ──────────────────────────────────────────────────────────────

export interface SendMessageRequest {
  message: string;
  bookingId: string;
  roomId: string;
  roomFloor: number;
  roomNumber?: string;
  customerName?: string;
}

export interface SendMessageResult {
  replyText: string;
  serviceRequestId?: string;
  ticketReference?: string;
  isEscalated: boolean;
  isFallback: boolean;
}

// ── Active Requests (Live Dashboard) ─────────────────────────────────────────

export interface ActiveRequestDto {
  serviceRequestId: string;
  title: string;
  serviceRequestStatus: RequestStatus;
  priority: Priority;
  createdAt: string;
  // StaffTask info
  staffTaskId?: string;
  taskStatus?: TaskStatus;
  assignedEmployeeName?: string;
  taskAcceptedAt?: string;
  taskCompletedAt?: string;
  // CSAT
  csatSubmitted: boolean;
}

// ── Escalate ──────────────────────────────────────────────────────────────────

export interface EscalateRequest {
  bookingId: string;
  roomFloor: number;
  roomNumber?: string;
}

export interface EscalateResult {
  message: string;
  taskId: string;
}

// ── CSAT ──────────────────────────────────────────────────────────────────────

export interface SubmitCsatRequest {
  serviceRequestId: string;
  rating: number;
  comment?: string;
}

// ── Local chat bubble (UI-only, not persisted as-is) ─────────────────────────

export interface ChatBubble {
  id: string;         // local UUID
  role: 'user' | 'assistant' | 'system';
  content: string;
  isStreaming?: boolean;
  createdAt: Date;
  ticketRef?: string;
  serviceRequestId?: string;
  isEscalated?: boolean;
  isFallback?: boolean;
}

// ── SignalR event payloads ────────────────────────────────────────────────────

export interface RequestStatusUpdate {
  serviceRequestId: string;
  newStatus: RequestStatus;
  taskStatus?: TaskStatus;
  assignedEmployeeName?: string;
}

// ── Phase 8A AI Concierge SSE & Lifecycle Types ──────────────────────────────

export type ConciergeRequestLifecycleStatus =
  | 'REQUEST_PENDING'
  | 'TASK_CREATED'
  | 'ASSIGNED'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'FAILED';

export interface ConciergeServiceRequestDto {
  requestId: string;
  eventId: string;
  conversationId?: string;
  customerId: string;
  roomNumber: string;
  requestType: string;
  description: string;
  priority: string;
  status: ConciergeRequestLifecycleStatus;
  taskId?: string;
  createdAt: string;
  updatedAt: string;
}

export interface ConciergeStreamContext {
  verifiedRoom?: string;
  bookingReference?: string;
  guestName?: string;
}

export interface ConciergeMessageItemDto {
  id: string;
  conversationId: string;
  role: string;
  content: string;
  eventId?: string;
  status?: string;
  metadataJson?: string;
  createdAt: string;
}

export interface ConciergeConversationHistoryDto {
  conversationId: string;
  customerId: string;
  messages: ConciergeMessageItemDto[];
}

