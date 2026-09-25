/**
 * SmartHotel Dashboard API Service
 * Consolidates dashboard operations (rooms, bookings, tasks, payments,
 * room-types, hotel profile, departments, employees, complaints, reviews)
 * using the shared resilient Axios client.
 */

import api from "@/lib/axios";
import {
complaintsListSchema,
dashboardBookingsListSchema,
departmentsListSchema,
floorViewListSchema,
hotelsListSchema,
reviewsListSchema,
roomsListSchema,
roomTypesListSchema,
staffTasksListSchema,
} from "@/schemas/dashboard.schema";
import { ZodError,type ZodType } from "zod";

// ── Types & DTOs ─────────────────────────────────────────────────────────────

export interface RoomDto {
  id: string;
  roomNumber: string;
  floor: number;
  status: number; // 0=Available, 1=Occupied, 2=Dirty, 3=InCleaning, 4=Inspected, 5=OutOfOrder
  roomTypeName: string;
}

export interface RoomCategoryGroupDto {
  roomTypeId: string;
  roomTypeName: string;
  rooms: RoomDto[];
}

export interface FloorViewDto {
  floor: number;
  totalRooms: number;
  statusCounts: Record<string, number>;
  categories: RoomCategoryGroupDto[];
}

export interface RoomTypeImageDto {
  id: string;
  roomTypeId: string;
  imageUrl: string;
  displayOrder: number;
  isPrimary: boolean;
}

export interface RoomTypeDto {
  id: string;
  hotelId: string;
  name: string;
  title: string;
  bedType: string;
  capacity: number;
  roomSizeSqFt: number;
  pricePerNight: number;
  cleaningFee: number;
  amenitiesFee: number;
  longDescription: string;
  highlights: string[];
  amenities: string[];
  cancellationPolicyText: string;
  isPublished: boolean;
  isActive: boolean;
  images: RoomTypeImageDto[];
}

export interface CreateRoomTypePayload {
  hotelId: string;
  name: string;
  title: string;
  bedType: string;
  capacity: number;
  roomSizeSqFt: number;
  pricePerNight: number;
  cleaningFee: number;
  amenitiesFee: number;
  longDescription: string;
  highlights: string[];
  amenities: string[];
  cancellationPolicyText: string;
}

export interface UpdateRoomTypePayload {
  name: string;
  title: string;
  bedType: string;
  capacity: number;
  roomSizeSqFt: number;
  pricePerNight: number;
  cleaningFee: number;
  amenitiesFee: number;
  longDescription: string;
  highlights: string[];
  amenities: string[];
  cancellationPolicyText: string;
  isActive: boolean;
}

export interface HotelDto {
  id: string;
  name: string;
  address: string;
  phone: string;
  email: string;
  totalFloors: number;
  totalRooms: number;
}

export interface UpdateHotelPayload {
  name: string;
  address: string;
  phone: string;
  email: string;
  totalFloors: number;
}

export interface DepartmentDto {
  id: string;
  name: string;
  description: string;
  managerId?: string | null;
  managerName?: string | null;
  employeeCount: number;
  employees: { id: string; fullName: string; email: string; role: string }[];
}

export interface CreateDepartmentPayload {
  name: string;
  description: string;
  managerId?: string | null;
}

export interface DashboardBookingDto {
  id: string;
  bookingReference?: string;
  customerName?: string;
  customerLastName?: string;
  customerEmail?: string;
  roomNumber: string;
  roomId: string;
  checkInDate: string;
  checkOutDate: string;
  status: number | string; // 0=PendingPayment, 1=Confirmed, 2=CheckedIn, 3=CheckedOut, 4=Cancelled
  totalAmount: number;
  paymentReference?: string;
  payHereOrderId?: string;
}

export interface RoomReadinessDto {roomId:string;roomStatus:string;maintenanceBlocked:boolean;housekeepingApproved:boolean;readinessStatus:string;readyForCheckIn:boolean;blocker?:string|null;}

export interface FrontOfficeAuditLogDto {
  id: string;
  bookingId: string;
  action: string;
  actorUserId?: string;
  actorRole: string;
  departmentCode: string;
  details?: string;
  timestampUtc: string;
}

export interface FrontOfficeSummaryDto {
  todayArrivals: number;
  todayDepartures: number;
  pendingCheckIns: number;
  activeOccupancy: number;
}

export interface GuestSearchResultDto {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  nationality: string;
  maskedNationalId?: string;
  loyaltyTier: string;
  role: string;
}

export interface StaffTaskDto {
  id: string;
  title: string;
  requiredRole: string; // Housekeeper, Maintenance, Admin, etc.
  targetFloor: number;
  priority: number; // 0=Low, 1=Medium, 2=High
  status: number; // 0=Pending, 1=Assigned, 2=Accepted, 3=InProgress, 4=Completed, 5=Cancelled
  assignedEmployeeName?: string;
}

export interface ComplaintTimelineEntryDto {
  id: string;
  fromStatus: number | string;
  toStatus: number | string;
  note: string;
  changedBy: string;
  timestampUtc: string;
}

export interface ComplaintDto {
  id: string;
  bookingId?: string | null;
  customerId: string;
  category: number | string;
  severity: number | string;
  status: number | string;
  title: string;
  description: string;
  slaDeadlineUtc: string;
  isOverdue?: boolean;
  resolutionNotes?: string | null;
  resolvedAtUtc?: string | null;
  createdAtUtc: string;
  timeline: ComplaintTimelineEntryDto[];
}

export interface UpdateComplaintStatusPayload {
  status: number;
  note?: string;
  resolutionNotes?: string;
}

export interface ReviewDto {
  id: string;
  bookingId: string;
  customerId: string;
  roomTypeId: string;
  rating: number;
  comment: string;
  isPublished: boolean;
  createdAtUtc: string;
}

// ── Employee DTOs ─────────────────────────────────────────────────────────────

export interface EmployeeCreateDto {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  role: string;
  /** "Active" | "PendingApproval" | "Rejected" */
  status: string;
  departmentId: string;
  departmentName?: string;
  isActive: boolean;
  createdAtUtc: string;
  createdByName?: string;
  rejectionReason?: string;
  reviewedAtUtc?: string;
  temporaryPassword?: string;
}

export interface ResendCredentialsDto {
  message: string;
  temporaryPassword: string;
}

export interface EmployeeProfileDto extends EmployeeCreateDto {
  nationalId?: string;
  nicPhotoUrl?: string;
  profilePhotoUrl?: string;
  bankName?: string;
  bankAccountName?: string;
  bankAccountNumber?: string;
  bankBranch?: string;
  suspensionReason?: string;
  suspendedAtUtc?: string;
}

export interface ApprovalRequestDto {
  id: string;
  type: string;
  description: string;
  requestedBy: string;
  status: "PendingApproval" | "Active" | "Rejected";
  createdAtUtc: string;
}

export interface OwnerMetricsDto {
  revenue: number;
  revenueTrend: number | null;
  occupancy: number;
  adr: number;
  profitMargin: number | null;
  guestSatisfaction: number | null;
}

export interface StaffOversightDto {
  managerId: string;
  managerName: string;
  departmentName: string;
  teamSize: number;
  taskCompletionRate: number | null;
}

// ── Query Parameters Interfaces ──────────────────────────────────────────────

export interface GetRoomsParams {
  status?: number;
  floor?: number;
  page?: number;
  pageSize?: number;
  signal?: AbortSignal;
}

export interface GetBookingsParams {
  status?: number;
  page?: number;
  pageSize?: number;
  search?: string;
  signal?: AbortSignal;
}

export interface GetTasksParams {
  signal?: AbortSignal;
}

export interface GetComplaintsParams {
  status?: number;
  severity?: number;
  overdueOnly?: boolean;
  signal?: AbortSignal;
}

export interface GetReviewsParams {
  isPublished?: boolean;
  signal?: AbortSignal;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function cleanQueryParameters(
  params?: Record<string, unknown>
): Record<string, unknown> | undefined {
  if (!params) return undefined;
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      result[key] = value;
    }
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

/**
 * Validates response data against a Zod schema.
 * If validation fails, logs detailed field paths, expected vs received,
 * and re-throws a clear Error to prevent processing unvalidated data.
 */
function parseApiResponse<T>(schema: ZodType<T>, data: unknown, endpoint: string): T {
  try {
    return schema.parse(data);
  } catch (err: unknown) {
    if (err instanceof ZodError) {
      const formattedIssues = err.issues.map((issue) => {
        const path = issue.path.length > 0 ? issue.path.join(".") : "(root)";
        const expectedReceived =
          "expected" in issue && "received" in issue
            ? ` (expected: ${String(issue.expected)}, received: ${String(issue.received)})`
            : "";
        return `${path}: ${issue.message}${expectedReceived}`;
      });

      console.error(
        `❌ [Schema Validation Error] ${endpoint}:`,
        formattedIssues,
        "\nRaw response payload:",
        data
      );

      throw new Error(
        `API response validation failed for ${endpoint}: ${formattedIssues.join("; ")}`
      );
    }
    throw err;
  }
}

// ── Service Implementation ───────────────────────────────────────────────────

export const dashboardApi = {
  // ── Rooms ───────────────────────────────────────────────────────────────────

  /**
   * Fetch rooms list with optional status/floor filtering, pagination, and cancellation signal.
   * Validates response against roomsListSchema at runtime.
   */
  async getRooms(params?: GetRoomsParams): Promise<RoomDto[]> {
    const { signal, ...queryParams } = params ?? {};
    const { data } = await api.get<unknown>("/api/v1/rooms", {
      params: cleanQueryParameters(queryParams),
      signal,
    });
    return parseApiResponse(roomsListSchema, data, "GET /api/v1/rooms");
  },

  /**
   * Get rooms grouped by floor for floor-view layout.
   */
  async getFloorView(signal?: AbortSignal): Promise<FloorViewDto[]> {
    const { data } = await api.get<unknown>("/api/v1/rooms/floor-view", { signal });
    return parseApiResponse(floorViewListSchema, data, "GET /api/v1/rooms/floor-view");
  },

  /**
   * Update room status (Available, Occupied, Cleaning, Maintenance, etc.).
   */
  async updateRoomStatus(
    roomId: string,
    newStatus: number,
    reason = "Updated via dashboard"
  ): Promise<void> {
    await api.patch(`/api/v1/rooms/${roomId}/status`, {
      newStatus,
      reason,
    });
  },

  // ── Room Types ──────────────────────────────────────────────────────────────

  /**
   * Fetch all room types (admin sees draft + published).
   */
  async getRoomTypes(signal?: AbortSignal): Promise<RoomTypeDto[]> {
    const { data } = await api.get<unknown>("/api/v1/room-types", { signal });
    return parseApiResponse(roomTypesListSchema, data, "GET /api/v1/room-types");
  },

  /**
   * Create a new draft room type (Admin only).
   */
  async createRoomType(payload: CreateRoomTypePayload): Promise<RoomTypeDto> {
    const { data } = await api.post<RoomTypeDto>("/api/v1/room-types", payload);
    return data;
  },

  /**
   * Update an existing room type (Admin only).
   */
  async updateRoomType(id: string, payload: UpdateRoomTypePayload): Promise<RoomTypeDto> {
    const { data } = await api.put<RoomTypeDto>(`/api/v1/room-types/${id}`, payload);
    return data;
  },

  /**
   * Publish a draft room type to make it visible to customers (Admin only).
   */
  async publishRoomType(id: string): Promise<void> {
    await api.post(`/api/v1/room-types/${id}/publish`);
  },

  /**
   * Get all images for a room type.
   */
  async getRoomTypeImages(roomTypeId: string, signal?: AbortSignal): Promise<RoomTypeImageDto[]> {
    const { data } = await api.get<RoomTypeImageDto[]>(
      `/api/v1/room-types/${roomTypeId}/images`,
      { signal }
    );
    return data;
  },

  /**
   * Upload an image to a room type (Admin only). Multipart form-data.
   */
  async uploadRoomTypeImage(
    roomTypeId: string,
    file: File,
    isPrimary = false,
    onUploadProgress?: (pct: number) => void
  ): Promise<RoomTypeImageDto> {
    const formData = new FormData();
    formData.append("file", file);
    const { data } = await api.post<RoomTypeImageDto>(
      `/api/v1/room-types/${roomTypeId}/images?isPrimary=${isPrimary}`,
      formData,
      {
        headers: { "Content-Type": "multipart/form-data" },
        onUploadProgress: (e) => {
          if (onUploadProgress && e.total) {
            onUploadProgress(Math.round((e.loaded * 100) / e.total));
          }
        },
      }
    );
    return data;
  },

  /**
   * Delete an image from a room type (Admin only).
   */
  async deleteRoomTypeImage(roomTypeId: string, imageId: string): Promise<void> {
    await api.delete(`/api/v1/room-types/${roomTypeId}/images/${imageId}`);
  },

  /**
   * Reorder images for a room type (Admin only).
   */
  async reorderRoomTypeImages(roomTypeId: string, imageIds: string[]): Promise<void> {
    await api.put(`/api/v1/room-types/${roomTypeId}/images/reorder`, imageIds);
  },

  // ── Hotel Profile ───────────────────────────────────────────────────────────

  /**
   * Fetch all hotels.
   */
  async getHotels(signal?: AbortSignal): Promise<HotelDto[]> {
    const { data } = await api.get<unknown>("/api/v1/hotels", { signal });
    return parseApiResponse(hotelsListSchema, data, "GET /api/v1/hotels");
  },

  /**
   * Update hotel details (Admin only).
   */
  async updateHotel(id: string, payload: UpdateHotelPayload): Promise<HotelDto> {
    const { data } = await api.put<HotelDto>(`/api/v1/hotels/${id}`, payload);
    return data;
  },

  // ── Departments ─────────────────────────────────────────────────────────────

  /**
   * Fetch all departments (optionally filtered by hotelId).
   */
  async getDepartments(hotelId?: string, signal?: AbortSignal): Promise<DepartmentDto[]> {
    const { data } = await api.get<unknown>("/api/v1/departments", {
      params: cleanQueryParameters({ hotelId }),
      signal,
    });
    return parseApiResponse(departmentsListSchema, data, "GET /api/v1/departments");
  },

  /**
   * Create a new department (Admin only).
   */
  async createDepartment(payload: CreateDepartmentPayload): Promise<DepartmentDto> {
    const { data } = await api.post<DepartmentDto>("/api/v1/departments", payload);
    return data;
  },

  async updateDepartment(id: string, payload: CreateDepartmentPayload): Promise<DepartmentDto> {
    const { data } = await api.put<DepartmentDto>(`/api/v1/departments/${id}`, payload);
    return data;
  },

  // ── Bookings ────────────────────────────────────────────────────────────────

  /**
   * Fetch bookings list with optional status/search filters, pagination, and cancellation signal.
   * Validates response against dashboardBookingsListSchema at runtime.
   */
  async getBookings(params?: GetBookingsParams): Promise<DashboardBookingDto[]> {
    const { signal, ...queryParams } = params ?? {};
    const { data } = await api.get<unknown>("/api/v1/bookings", {
      params: cleanQueryParameters(queryParams),
      signal,
    });
    return parseApiResponse(dashboardBookingsListSchema, data, "GET /api/v1/bookings");
  },

  /**
   * Check in a guest for confirmed booking.
   */
  async checkInGuest(bookingId: string): Promise<void> {
    await api.post(`/api/v1/bookings/${bookingId}/check-in`);
  },

  /**
   * Check out a guest.
   */
  async checkOutGuest(bookingId: string): Promise<void> {
    await api.post(`/api/v1/bookings/${bookingId}/check-out`);
  },

  async getRoomReadiness(roomId:string):Promise<RoomReadinessDto>{return (await api.get<RoomReadinessDto>(`/api/v1/rooms/${roomId}/readiness`)).data;},

  /**
   * Reassign a reservation to a different room.
   */
  async assignRoom(bookingId: string, newRoomId: string, reason?: string): Promise<void> {
    await api.post(`/api/v1/bookings/${bookingId}/assign-room`, {
      newRoomId,
      reason,
    });
  },

  /**
   * Front Office staff cancellation override (requires Front Office Manager role and reason).
   */
  async cancelBookingStaff(bookingId: string, reason: string): Promise<void> {
    await api.post(`/api/v1/bookings/${bookingId}/cancel`, {
      reason,
    });
  },

  /**
   * Get immutable audit history for a booking.
   */
  async getBookingAuditLogs(bookingId: string): Promise<FrontOfficeAuditLogDto[]> {
    const { data } = await api.get<FrontOfficeAuditLogDto[]>(`/api/v1/bookings/${bookingId}/audit-logs`);
    return data;
  },

  /**
   * Get Front Office operational summary metrics.
   */
  async getFrontOfficeSummary(): Promise<FrontOfficeSummaryDto> {
    const { data } = await api.get<FrontOfficeSummaryDto>("/api/v1/bookings/frontoffice/summary");
    return data;
  },

  /**
   * Search guests directory with data minimization and masked national IDs.
   */
  async searchGuests(search?: string, limit?: number): Promise<GuestSearchResultDto[]> {
    const { data } = await api.get<GuestSearchResultDto[]>("/api/guests", {
      params: cleanQueryParameters({ search, limit }),
    });
    return data;
  },

  /**
   * Manually confirm payment for a booking (e.g., bank transfer or cash at front desk).
   */
  async confirmPayment(
    bookingId: string,
    reference: string,
    notes = "Confirmed by admin in dashboard"
  ): Promise<void> {
    await api.post(`/api/v1/payments/${bookingId}/confirm`, {
      paymentReference: reference,
      notes,
    });
  },

  // ── Staff Tasks ─────────────────────────────────────────────────────────────

  /**
   * Fetch staff tasks with optional cancellation signal.
   * Validates response against staffTasksListSchema at runtime.
   */
  async getTasks(params?: GetTasksParams): Promise<StaffTaskDto[]> {
    const { data } = await api.get<unknown>("/api/v1/tasks", {
      signal: params?.signal,
    });
    return parseApiResponse(staffTasksListSchema, data, "GET /api/v1/tasks");
  },

  /**
   * Update task state (accept, start, complete, reject).
   */
  async updateTaskStatus(taskId: string, action: string): Promise<void> {
    await api.post(`/api/v1/tasks/${taskId}/${action}`);
  },

  // ── Complaints ──────────────────────────────────────────────────────────────

  /**
   * Fetch all complaints (Admin/Manager/Receptionist).
   */
  async getComplaints(params?: GetComplaintsParams): Promise<ComplaintDto[]> {
    const { signal, ...queryParams } = params ?? {};
    const { data } = await api.get<unknown>("/api/v1/complaints", {
      params: cleanQueryParameters(queryParams),
      signal,
    });
    return parseApiResponse(complaintsListSchema, data, "GET /api/v1/complaints");
  },

  /**
   * Update complaint status (Admin/Manager/Receptionist).
   */
  async updateComplaintStatus(
    complaintId: string,
    payload: UpdateComplaintStatusPayload
  ): Promise<ComplaintDto> {
    const { data } = await api.patch<ComplaintDto>(
      `/api/v1/complaints/${complaintId}/status`,
      payload
    );
    return data;
  },

  // ── Reviews ─────────────────────────────────────────────────────────────────

  /**
   * Fetch all reviews for moderation (Admin/Manager).
   */
  async getAllReviews(params?: GetReviewsParams): Promise<ReviewDto[]> {
    const { signal, ...queryParams } = params ?? {};
    const { data } = await api.get<unknown>("/api/v1/reviews", {
      params: cleanQueryParameters(queryParams),
      signal,
    });
    return parseApiResponse(reviewsListSchema, data, "GET /api/v1/reviews");
  },

  /**
   * Hide a review from public display (Admin/Manager).
   */
  async hideReview(reviewId: string): Promise<void> {
    await api.post(`/api/v1/reviews/${reviewId}/hide`);
  },

  /**
   * Unhide a previously hidden review (Admin/Manager).
   */
  async unhideReview(reviewId: string): Promise<void> {
    await api.post(`/api/v1/reviews/${reviewId}/unhide`);
  },

  // ── Guest Role ──────────────────────────────────────────────────────────────

  /**
   * Update customer role (Admin-only by convention; no server-side guard).
   */
  async updateCustomerRole(customerId: string, role: string): Promise<void> {
    await api.put(`/api/guests/${customerId}/role`, { role });
  },

  // ── Employee Management ─────────────────────────────────────────────────────

  /**
   * Create a new employee.
   * Admin → status = Active immediately (any role allowed).
   * Manager → status = PendingApproval (non-Manager roles only — enforced server-side).
   */
  async createEmployee(payload: {
    firstName: string;
    lastName: string;
    email: string;
    password?: string;
    contactEmail?: string;
    role?: string;
    departmentId: string;
  }): Promise<EmployeeCreateDto> {
    const { data } = await api.post<EmployeeCreateDto>("/api/v1/employees", payload);
    return data;
  },

  async getEmployees(): Promise<EmployeeCreateDto[]> {
    const { data } = await api.get<EmployeeCreateDto[]>("/api/v1/employees");
    return data;
  },

  /**
   * Admin-only: fetch all employees with status = PendingApproval.
   */
  async getPendingEmployees(): Promise<EmployeeCreateDto[]> {
    const { data } = await api.get<EmployeeCreateDto[]>("/api/v1/employees/pending");
    return data;
  },

  /**
   * Admin-only: approve a pending employee.
   */
  async approveEmployee(employeeId: string): Promise<EmployeeCreateDto> {
    const { data } = await api.post<EmployeeCreateDto>(`/api/v1/employees/${employeeId}/approve`);
    return data;
  },

  /**
   * Admin-only: reject a pending employee with optional reason.
   */
  async rejectEmployee(employeeId: string, reason?: string): Promise<EmployeeCreateDto> {
    const { data } = await api.post<EmployeeCreateDto>(`/api/v1/employees/${employeeId}/reject`, { reason });
    return data;
  },

  async resendEmployeeCredentials(employeeId: string): Promise<ResendCredentialsDto> {
    const { data } = await api.post<ResendCredentialsDto>(`/api/v1/employees/${employeeId}/resend-credentials`);
    return data;
  },

  async getEmployeeProfile(employeeId: string): Promise<EmployeeProfileDto> {
    const { data } = await api.get<EmployeeProfileDto>(`/api/v1/employees/${employeeId}/profile`);
    return data;
  },

  async updateEmployeeProfile(employeeId: string, payload: Partial<EmployeeProfileDto>): Promise<EmployeeProfileDto> {
    const { data } = await api.put<EmployeeProfileDto>(`/api/v1/employees/${employeeId}/profile`, payload);
    return data;
  },

  async suspendEmployee(employeeId: string, reason: string): Promise<EmployeeProfileDto> {
    const { data } = await api.post<EmployeeProfileDto>(`/api/v1/employees/${employeeId}/suspend`, { reason });
    return data;
  },

  async reactivateEmployee(employeeId: string): Promise<EmployeeProfileDto> {
    const { data } = await api.post<EmployeeProfileDto>(`/api/v1/employees/${employeeId}/reactivate`);
    return data;
  },

  async deleteEmployee(employeeId: string): Promise<void> {
    await api.delete(`/api/v1/employees/${employeeId}`);
  },

  async getApprovalRequests(): Promise<ApprovalRequestDto[]> {
    const { data } = await api.get<ApprovalRequestDto[]>("/api/v1/approval-requests");
    return data;
  },

  async approveApprovalRequest(id: string): Promise<ApprovalRequestDto> {
    const { data } = await api.post<ApprovalRequestDto>(`/api/v1/approval-requests/${id}/approve`);
    return data;
  },

  async rejectApprovalRequest(id: string): Promise<ApprovalRequestDto> {
    const { data } = await api.post<ApprovalRequestDto>(`/api/v1/approval-requests/${id}/reject`);
    return data;
  },

  async getOwnerMetrics(): Promise<OwnerMetricsDto> {
    const [bookings, rooms, reviews] = await Promise.all([
      this.getBookings(),
      this.getRooms(),
      this.getAllReviews(),
    ]);
    const now = new Date();
    const currentMonth = now.getUTCMonth();
    const currentYear = now.getUTCFullYear();
    const previous = new Date(Date.UTC(currentYear, currentMonth - 1, 1));
    const isPaid = (status: number | string) => Number(status) >= 1 && Number(status) !== 4;
    const currentRevenue = bookings.filter((booking) => {
      const date = new Date(booking.checkInDate);
      return isPaid(booking.status) && date.getUTCMonth() === currentMonth && date.getUTCFullYear() === currentYear;
    }).reduce((sum, booking) => sum + booking.totalAmount, 0);
    const previousRevenue = bookings.filter((booking) => {
      const date = new Date(booking.checkInDate);
      return isPaid(booking.status) && date.getUTCMonth() === previous.getUTCMonth() && date.getUTCFullYear() === previous.getUTCFullYear();
    }).reduce((sum, booking) => sum + booking.totalAmount, 0);
    const paidBookings = bookings.filter((booking) => isPaid(booking.status));
    const occupiedRooms = rooms.filter((room) => Number(room.status) === 1).length;

    return {
      revenue: currentRevenue,
      revenueTrend: previousRevenue > 0 ? ((currentRevenue - previousRevenue) / previousRevenue) * 100 : null,
      occupancy: rooms.length ? (occupiedRooms / rooms.length) * 100 : 0,
      adr: paidBookings.length ? paidBookings.reduce((sum, booking) => sum + booking.totalAmount, 0) / paidBookings.length : 0,
      profitMargin: null,
      guestSatisfaction: reviews.length ? reviews.reduce((sum, review) => sum + review.rating, 0) / reviews.length : null,
    };
  },

  async getStaffOversight(): Promise<StaffOversightDto[]> {
    const [employees, departments, tasks] = await Promise.all([this.getEmployees(), this.getDepartments(), this.getTasks()]);
    return employees.filter((employee) => employee.role === "Manager").map((manager) => {
      const department = departments.find((item) => item.id === manager.departmentId);
      const teamNames = new Set(employees.filter((employee) => employee.departmentId === manager.departmentId).map((employee) => employee.fullName));
      const teamTasks = tasks.filter((task) => task.assignedEmployeeName && teamNames.has(task.assignedEmployeeName));
      return {
        managerId: manager.id,
        managerName: manager.fullName,
        departmentName: manager.departmentName ?? department?.name ?? "Unassigned",
        teamSize: department?.employeeCount ?? teamNames.size,
        taskCompletionRate: teamTasks.length ? (teamTasks.filter((task) => Number(task.status) === 4).length / teamTasks.length) * 100 : null,
      };
    });
  },

  /**
   * Admin-only: update an active employee's role.
   */
  async updateEmployeeRole(employeeId: string, role: string): Promise<EmployeeCreateDto> {
    const { data } = await api.put<EmployeeCreateDto>(`/api/v1/employees/${employeeId}/role`, { role });
    return data;
  },
};

export default dashboardApi;
